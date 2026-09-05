using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The protocol layer: every tool this server exposes, and nothing else. It is one class across
    /// many files, split by area the same way <c>Portal</c> is, and for the same reason -- it was
    /// 2,176 lines against this repository's limit of 300.
    ///
    /// <list type="bullet">
    /// <item>McpServer -- the connection, the session's state, and the services the tools share</item>
    /// <item>McpServerProject -- what is open, its tree, its snapshots and backups</item>
    /// <item>McpServerDevices -- hardware and the software on it</item>
    /// <item>McpServerBlocks, McpServerTypes, McpServerDocuments -- reading and exporting the program</item>
    /// <item>McpServerSimulation, McpServerNetwork -- reading PLCSIM Advanced and the network</item>
    /// <item>McpServerJobs, McpServerCell -- polling long operations, and expanding a cell</item>
    /// <item>McpServerWrites -- everything that changes anything</item>
    /// </list>
    ///
    /// **The last line of that list is the one that is not about size.** A tool that changes
    /// anything goes through <c>GuardedTool.Run</c>, names its target through <c>ChangeTarget</c>,
    /// and gets a test in <c>Test16GuardedWrites</c>. A write tool that forgets the guard passes
    /// every other test in the suite, so the separation is kept visible as a file: a new tool that
    /// writes and lands anywhere but McpServerWrites is a review finding on sight.
    ///
    /// The attribute below is on this partial only. It may appear once per class, and the tools in
    /// the other files are found through it regardless of which file they are written in.
    /// </remarks>
    [McpServerToolType]
    public static partial class McpServer
    {
        private const int SummaryLength = 120;

        private static IServiceProvider? _services;
        private static Portal? _portal;
        private static Siemens.SimulationRuntime? _simulation;

        public static ILogger? Logger { get; set; }

        // CA1065 says a property getter must not throw, and it is right about accidental throws.
        // Here the throw is the feature: this is the single point every tool passes through to reach
        // TIA Portal, so it is the only place a missing Openness gate can be caught at all. The
        // alternative considered was making it a method, which would have put RequirePortal() in front
        // of fifty-four call sites to satisfy a rule about a different problem. Suppressed here and
        // nowhere else.
#pragma warning disable CA1065
        public static Portal Portal
        {
            get
            {
                // Enforcement, not decoration. Every tool reaches TIA Portal through this property,
                // which makes it the one place that can catch a tool that forgot the gate. Without
                // the check such a tool would work perfectly until the day a job happened to be
                // running at the same time, and the failure would be a corrupted project rather
                // than an exception. With it the omission fails on the first call, in the suite.
                if (!Siemens.OpennessGate.IsHeldByCurrentThread)
                {
                    throw new PortalException(
                        PortalErrorCode.InvalidState,
                        "TIA Portal was reached without holding the Openness gate. The calling tool is "
                        + "missing 'using var openness = OpennessGate.Enter();' as its first statement. "
                        + "See OpennessGate for why one call at a time is not optional.");
                }

                if (_services != null)
                {
                    return _services.GetRequiredService<Portal>();
                }

                return _portal ??= new Portal();
            }
            set
            {
                _portal = value ?? throw new ArgumentNullException(nameof(value), "Portal cannot be null");
            }
        }
#pragma warning restore CA1065

        /// <summary>
        /// The one simulation runtime the server shares.
        /// </summary>
        /// <remarks>
        /// Shared rather than built per call. A PLCSIM Advanced controller stays registered only
        /// while a handle to it is open, so a runtime created inside a tool method would release
        /// its handles when the method returned and the controller from CreateSimulationInstance
        /// would be gone before DownloadToSimulation ran. Measured on 2026-08-17: an unheld
        /// controller unregisters itself within fifteen seconds.
        /// </remarks>
        public static Siemens.SimulationRuntime Simulation
        {
            get
            {
                if (_services != null)
                {
                    return _services.GetRequiredService<Siemens.SimulationRuntime>();
                }

                return _simulation ??= new Siemens.SimulationRuntime(Logger);
            }
        }

        /// <summary>
        /// The governance layer this session writes through.
        /// </summary>
        /// <remarks>
        /// Shared, because plans awaiting confirmation live in it: a per-call guard would lose the
        /// plan between proposing a change and confirming it.
        /// </remarks>
        public static Governance.GuardedWrite GuardedWrites =>
            _services != null
                ? _services.GetRequiredService<Governance.GuardedWrite>()
                : Fallback.Guard;

        /// <summary>The mode this session is in.</summary>
        public static Governance.IModeGate ModeGate =>
            _services != null
                ? _services.GetRequiredService<Governance.IModeGate>()
                : Fallback.Gate;

        /// <summary>Where long operations run, so the caller is not blocked on them.</summary>
        /// <remarks>
        /// Shared, because a job outlives the call that started it: a per-call store would lose the
        /// job between starting it and asking how it went.
        /// </remarks>
        public static Jobs.IJobStore JobStore =>
            _services != null
                ? _services.GetRequiredService<Jobs.IJobStore>()
                : Fallback.JobStore;

        /// <summary>Where the previous state of anything this session overwrites is kept.</summary>
        /// <remarks>
        /// Deliberately not a parameter on the write tools any more. A caller that chooses where the
        /// backup goes is a caller who can put it somewhere nobody will look, and an agent that can
        /// choose can choose a temp directory. The tools ask this for a location and are told one.
        /// </remarks>
        public static Governance.IBackupRegistry Backups =>
            _services != null
                ? _services.GetRequiredService<Governance.IBackupRegistry>()
                : Fallback.Backups;

        /// <summary>
        /// The governance layer a host that registered none still writes through.
        /// </summary>
        /// <remarks>
        /// There is no unguarded path, so there cannot be a "no governance registered" one either.
        /// Refusing with an exception would have reached the caller as an operation failure —
        /// something to retry — when the truth is that this session may not write. So the fallback
        /// is a real gate in Study Mode reading the default policy, which is absent on a machine
        /// that never configured one and therefore denies everything, loudly and with a reason.
        /// </remarks>
        private static class Fallback
        {
            internal static readonly Governance.IModeGate Gate = Governance.ModeGate.ForStudy();

            internal static readonly Governance.GuardedWrite Guard = new Governance.GuardedWrite(
                Gate,
                Governance.WritePolicy.Load(CliOptions.DefaultPolicyPath),
                new Governance.JsonlAuditTrail(CliOptions.DefaultAuditPath),
                new Governance.ChangePlanStore(new Governance.SystemClock()),

                // Nothing was configured on this path — that is what makes it the fallback — so
                // there is no index to point at and no path to guess. Plans made here say the
                // hardware context is unavailable, which is true and is the point.
                new Knowledge.UnavailableHardwareLookup());

            internal static readonly Governance.IBackupRegistry Backups = new Governance.BackupRegistry(
                CliOptions.DefaultBackupRoot,
                new Governance.SystemClock());

            internal static readonly Jobs.IJobStore JobStore = new Jobs.JobStore(
                new Governance.SystemClock(),
                new Jobs.ThreadPoolJobDispatcher());
        }

        public static void SetServiceProvider(IServiceProvider services)
        {
            _services = services;
        }

        [McpServerTool(Name = "GetOperationMode"), Description("Report whether this session targets PLCSIM Advanced (Study) or physical hardware (Workshop), and whether changes confirm themselves or need a person. Ask this before proposing any write: the answer decides what happens next.")]
        public static ResponseMessage GetOperationMode()
        {
            try
            {
                var gate = ModeGate;

                return new ResponseMessage
                {
                    Message =
                        $"Mode: {gate.Mode}. Confirmation: {gate.RequiredConfirmation}. " +
                        (gate.RequiredConfirmation == Governance.Confirmation.Manual
                            ? "Every change waits for a person to confirm it, one at a time."
                            : "Whitelisted changes confirm themselves and are still audited."),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["mode"] = gate.Mode.ToString(),
                        ["confirmation"] = gate.RequiredConfirmation.ToString()
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the operation mode");
            }
        }

        #region portal

        [McpServerTool(Name = "Connect"), Description("Connect to TIA-Portal")]
        public static ResponseConnect Connect()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            Logger?.LogInformation("Connecting to TIA Portal...");

            try
            {
                if (Portal.ConnectPortal())
                {
                    return new ResponseConnect
                    {
                        Message = "Connected to TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to connect to TIA-Portal", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting to TIA-Portal: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "Disconnect"), Description("Disconnect from TIA-Portal")]
        public static ResponseDisconnect Disconnect()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                if (Portal.DisconnectPortal())
                {
                    return new ResponseDisconnect
                    {
                        Message = "Disconnected from TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed disconnecting from TIA-Portal", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error disconnecting from TIA-Portal: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region state

        [McpServerTool(Name = "GetState"), Description("Get the state of the TIA-Portal MCP server")]
        public static ResponseState GetState()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var state = Portal.GetState();

                if (state != null)
                {
                    return new ResponseState
                    {
                        Message = "TIA-Portal MCP server state retrieved",
                        IsConnected = state.IsConnected,
                        Project = state.Project,
                        Session = state.Session,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to retrieve TIA-Portal MCP server state", McpErrorCode.InternalError);
                }
                
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving TIA-Portal MCP server state: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region project/session

        #region simulation

        #endregion

        private static McpException ToMcpException(TiaMcpServer.Siemens.PortalException portalException, string fallbackMessage)
        {
            Logger?.LogError(portalException, "{Message}", fallbackMessage);

            switch (portalException.Code)
            {
                case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                    return new McpException(portalException.Message, McpErrorCode.InvalidParams);

                default:
                    return new McpException($"{fallbackMessage}. Reason: {portalException.Message}", McpErrorCode.InternalError);
            }
        }

        #endregion

        #region devices

        #endregion

        #region plc software

        #endregion

        #region blocks

        #endregion

        #region types

        #endregion

        #region documents

        #endregion
    }
}
