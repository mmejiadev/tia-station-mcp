using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
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
                new Governance.ChangePlanStore(new Governance.SystemClock()));

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

        [McpServerTool(Name = "GetProject"), Description("Get open local project/session")]
        public static ResponseGetProjects GetProjects()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetProjects();

                list.AddRange(Portal.GetSessions());

                var responseList = new List<ResponseProjectInfo>();
                foreach (var project in list)
                {
                    var attributes = Helper.GetAttributeList(project);

                    if (project != null)
                    {
                        responseList.Add(new ResponseProjectInfo
                        {
                            Name = project.Name,
                            Attributes = attributes
                        });
                    }
                }

                return new ResponseGetProjects
                {
                    Message = "Open projects and sessions retrieved",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving open projects: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "OpenProject"), Description("Open a TIA-Portal local project/session")]
        public static ResponseOpenProject OpenProject(
            [Description("path: defines the path where to the project/session")] string path)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                Portal.CloseProject();

                // get project extension
                string extension = Path.GetExtension(path).ToLowerInvariant();

                // use regex to check if extension is .ap\d+ or .als\d+
                if (!Regex.IsMatch(extension, @"^\.ap\d+$") &&
                    !Regex.IsMatch(extension, @"^\.als\d+$"))
                {
                    throw new McpException("Invalid project file extension. Use .apXX for projects or .alsXX for sessions, where XX=18,19,20,....", McpErrorCode.InvalidParams);
                }

                bool success = false;

                if (extension.StartsWith(".ap"))
                {
                    success = Portal.OpenProject(path);
                }
                if (extension.StartsWith(".als"))
                {
                    success = Portal.OpenSession(path);
                }

                if (success)
                {
                    return new ResponseOpenProject
                    {
                        Message = $"Project '{path}' opened",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed to open project '{path}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening project '{path}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "RetrieveProject"), Description("Retrieve a TIA-Portal project archive (.zapXX) into a directory and open it")]
        public static ResponseRetrieveProject RetrieveProject(
            [Description("archivePath: full path of the .zapXX archive to retrieve")] string archivePath,
            [Description("targetDirectory: directory to extract into; the call is refused if the project folder already exists")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var projectPath = Portal.RetrieveProject(archivePath, targetDirectory);

                return new ResponseRetrieveProject(projectPath)
                {
                    Message = $"Archive '{archivePath}' retrieved to '{projectPath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to retrieve archive '{archivePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving archive '{archivePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetNetworkTopology"), Description("List every device interface in the project with its address and subnet. Read this before writing code that addresses remote IO, or before configuring a PROFINET or PROFIBUS network: an interface with no subnet is wired to nothing.")]
        public static ResponseNetworkTopology GetNetworkTopology()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var nodes = Portal.GetNetworkTopology();

                var lines = nodes
                    .Select(node => $"{node.DevicePath} | {node.InterfaceName} | {node.NetworkType} | {node.Address} | {(node.IsConnected ? node.SubnetName : "<not connected>")}")
                    .ToList();

                var unconnected = nodes.Count(node => !node.IsConnected);

                return new ResponseNetworkTopology(lines)
                {
                    Message = unconnected == 0
                        ? $"{nodes.Count} interface(s), all connected to a subnet"
                        : $"{nodes.Count} interface(s), {unconnected} not connected to any subnet",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the network topology");
            }
        }

        [McpServerTool(Name = "GetOpcUaInterfaces"), Description("List the OPC UA server interfaces a CPU publishes, with whether each is enabled. An empty list means the CPU publishes nothing over OPC UA, which is the usual reason a client cannot connect.")]
        public static ResponseNetworkTopology GetOpcUaInterfaces(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var interfaces = Portal.GetOpcUaInterfaces(softwarePath);

                var lines = interfaces
                    .Select(item => $"{item.Name} | {(item.IsEnabled ? "enabled" : "disabled")} | {item.Author} | {item.LastModified:yyyy-MM-dd}")
                    .ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = interfaces.Count == 0
                        ? $"'{softwarePath}' publishes no OPC UA server interface"
                        : $"{interfaces.Count} OPC UA interface(s), {interfaces.Count(item => item.IsEnabled)} enabled",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to list the OPC UA interfaces of '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "ExportOpcUaInterface"), Description("Export one OPC UA server interface to a file. The interface is the contract between the PLC and every OPC UA client, so it belongs under version control alongside the code.")]
        public static ResponseMessage ExportOpcUaInterface(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("interfaceName: the server interface to export; call GetOpcUaInterfaces to see the names")] string interfaceName,
            [Description("exportPath: file to write")] string exportPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var written = Portal.ExportOpcUaInterface(softwarePath, interfaceName, exportPath);

                return new ResponseMessage
                {
                    Message = $"OPC UA interface '{interfaceName}' exported to '{written}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to export the OPC UA interface '{interfaceName}'");
            }
        }

        #region simulation

        [McpServerTool(Name = "ListSimulationInstances"), Description("List the PLCSIM Advanced virtual controllers and the runtime's network mode. Call this before downloading: a controller with no address, or a runtime in the wrong network mode, is why a download cannot connect.")]
        public static ResponseSimulationInstances ListSimulationInstances()
        {
            try
            {
                var instances = Simulation.ListInstances();

                return new ResponseSimulationInstances(instances.Select(ToResponse).ToList(), TiaMcpServer.Siemens.SimulationRuntime.NetworkMode)
                {
                    Message = $"{instances.Count} simulation instance(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to list simulation instances");
            }
        }

        [McpServerTool(Name = "ListSimulationTags"), Description("List the tags of the program a virtual controller holds: the names ReadSimulationTags and WriteSimulationTag take. The list is read from the controller and not from the project, so it is empty until something has been downloaded. Filter by name — a CPU has thousands of tags.")]
        public static ResponseSimulationTags ListSimulationTags(
            [Description("instanceName: the virtual controller to ask")] string instanceName,
            [Description("nameFilter: case-insensitive substring the tag name must contain, e.g. 'PieceId'. Omit for all of them.")] string? nameFilter = null,
            [Description("limit: how many tags to return at most")] int limit = TiaMcpServer.Siemens.SimulationRuntime.DefaultTagLimit)
        {
            // No Openness gate: this reads the controller through the PLCSIM API and never touches
            // TIA Portal, so queueing it behind a running compile would cost time and buy nothing.
            try
            {
                var tags = Simulation.ListTags(instanceName, nameFilter, limit);

                return new ResponseSimulationTags(tags.Items.Select(ToResponse).ToList(), tags.MatchCount, tags.TotalCount)
                {
                    Message = DescribeTagList(tags),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to list the tags of '{instanceName}'");
            }
        }

        [McpServerTool(Name = "ReadSimulationTags"), Description("Read tags of a running virtual controller, several in one call. This is how a downloaded program is observed: call ListSimulationTags first to get the exact names. A struct or an array has no value of its own — read its members.")]
        public static ResponseSimulationTagValues ReadSimulationTags(
            [Description("instanceName: the virtual controller to read from")] string instanceName,
            [Description("tagNames: the tag names, spelled exactly as ListSimulationTags reports them. A data block member has no quotes: DB_Cell.Feeder.Step")] string[] tagNames)
        {
            try
            {
                var values = Simulation.ReadTags(instanceName, tagNames);

                return new ResponseSimulationTagValues(values.Select(ToResponse).ToList())
                {
                    Message = $"{values.Count} tag(s) read from '{instanceName}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read tags of '{instanceName}'");
            }
        }

        [McpServerTool(Name = "GetSimulationTargetName"), Description("Report which PC interface a download would go through, without downloading. It is always a PLCSIM interface: this server refuses to download through a real network adapter.")]
        public static ResponseMessage GetSimulationTargetName(
            [Description("softwarePath: full path to the CPU in the project, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = Portal.GetSimulationTargetName(softwarePath);

                return new ResponseMessage
                {
                    Message = $"A download of '{softwarePath}' would go through: {target}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to resolve a download target for '{softwarePath}'");
            }
        }

        private static ResponseSimulationInstance ToResponse(TiaMcpServer.Siemens.SimulationInstanceInfo instance)
        {
            return new ResponseSimulationInstance(instance.Name, instance.OperatingState, instance.CpuType, instance.IpAddresses);
        }

        private static ResponseSimulationTag ToResponse(TiaMcpServer.Siemens.SimulationTagInfo tag)
        {
            return new ResponseSimulationTag(tag.Name, tag.Area, tag.DataType, tag.IsReadable);
        }

        private static ResponseSimulationTagValue ToResponse(TiaMcpServer.Siemens.SimulationTagValue value)
        {
            return new ResponseSimulationTagValue(value.Name, value.DataType, value.Value);
        }

        /// <summary>Says how much of the tag list the caller is looking at.</summary>
        private static string DescribeTagList(TiaMcpServer.Siemens.SimulationTagList tags)
        {
            if (tags.TotalCount == 0)
            {
                return "This controller holds no program, so it has no tags. Download one first.";
            }

            if (tags.IsTruncated)
            {
                return $"{tags.Items.Count} of {tags.MatchCount} matching tag(s), out of {tags.TotalCount}. Narrow the filter or raise the limit.";
            }

            return $"{tags.Items.Count} matching tag(s), out of {tags.TotalCount}";
        }

        private static ResponseSimulationInstance Describe(TiaMcpServer.Siemens.SimulationInstanceInfo instance, string message)
        {
            var response = ToResponse(instance);

            response.Message = message;
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = true
            };

            return response;
        }

        #endregion

        [McpServerTool(Name = "GetJobStatus"), Description("Ask how a long operation started with runAsJob is going. State is Queued, Running, Succeeded, Failed or Cancelled; detail carries the result once it has finished.")]
        public static ResponseJob GetJobStatus(
            [Description("jobId: the id the tool that started the job returned")] string jobId)
        {
            try
            {
                return Describe(JobStore.Status(Jobs.JobId.Parse(jobId)), "Job");
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to report on job '{jobId}'");
            }
        }

        [McpServerTool(Name = "CancelJob"), Description("Cancel a long operation that has not started yet. A job already inside Openness cannot be interrupted — a compile and a download accept no cancellation — and this reports that rather than pretending to stop it.")]
        public static ResponseJob CancelJob(
            [Description("jobId: the id the tool that started the job returned")] string jobId)
        {
            try
            {
                var job = JobStore.Cancel(Jobs.JobId.Parse(jobId));

                return Describe(
                    job,
                    job.State == Jobs.JobState.Cancelled
                        ? "Cancelled"
                        : "Not cancelled; it is past the point where that is possible. Job");
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to cancel job '{jobId}'");
            }
        }

        [McpServerTool(Name = "ListJobs"), Description("List every long operation this session has run, newest first, with what became of it.")]
        public static ResponseJobs ListJobs()
        {
            try
            {
                var items = JobStore.List().Select(ToResponse).ToList();

                return new ResponseJobs(items)
                {
                    Message = items.Count == 0
                        ? "No long operations have been started in this session."
                        : $"{items.Count} job(s), newest first.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = items.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to list the jobs");
            }
        }

        private static ResponseJob ToResponse(Jobs.JobRecord job)
        {
            return new ResponseJob(
                job.Id.Value,
                job.Tool,
                job.Target,
                job.State.ToString(),
                job.Detail,
                job.IsCancellable);
        }

        private static ResponseJob Describe(Jobs.JobRecord job, string prefix)
        {
            var response = ToResponse(job);

            response.Message = $"{prefix} '{job.Id}' ({job.Tool} on '{job.Target}'): {job.State}." +
                (job.Detail.Length == 0 ? string.Empty : $" {job.Detail}");
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = job.State != Jobs.JobState.Failed,
                ["jobId"] = job.Id.Value,
                ["state"] = job.State.ToString(),
                ["isFinished"] = job.IsFinished,
                ["isCancellable"] = job.IsCancellable
            };

            return response;
        }

        [McpServerTool(Name = "ExpandCellScl"), Description("Generate the SCL for a cell from its specification in spec/cells/ and the patterns in spec/patterns/. Returns the source without writing anything; pass Scl to WriteScl to put it in a project, then CompileSoftware. Station pattern first, coordinator second, because the coordinator instantiates the station.")]
        public static ResponseCellScl ExpandCellScl(
            [Description("cellPath: path to the cell specification, e.g. 'spec/cells/two-station-demo.json'")] string cellPath,
            [Description("patternDirectory: where station.scl.tmpl and coordinator.scl.tmpl live, default 'spec/patterns'")] string patternDirectory = "spec/patterns",
            [Description("includeEntryPoint: also generate the cell's instance data block and a Main OB that calls it every scan. Needed for the cell to run at all, and for a tag write to reach it — but it REPLACES the project's existing Main. False by default.")] bool includeEntryPoint = false)
        {
            // No Openness gate, deliberately. This reads two text files and does string work; it
            // never touches TIA Portal, so taking the gate would queue it behind a running compile
            // for no reason. See OpennessGate: only the tools that reach the portal take it.
            try
            {
                var cell = Spec.CellSpecificationFile.Load(cellPath);
                var expander = new Spec.SclTemplateExpander();

                var stationPattern = expander.Expand(ReadPattern(patternDirectory, "station.scl.tmpl"), cell);
                var coordinator = expander.Expand(ReadPattern(patternDirectory, "coordinator.scl.tmpl"), cell);
                var scl = stationPattern + Environment.NewLine + coordinator;

                if (includeEntryPoint)
                {
                    // Last, and it has to be: the data block is an instance of the coordinator, so
                    // a source declaring it before the block it instantiates does not compile.
                    scl += Environment.NewLine + expander.Expand(ReadPattern(patternDirectory, "main.scl.tmpl"), cell);
                }

                return new ResponseCellScl(cell.Name, cell.Stations.Select(item => item.Name).ToList(), scl)
                {
                    Message =
                        $"Generated SCL for cell {cell.Name} with {cell.Stations.Count} station(s)"
                        + (includeEntryPoint ? ", including a Main OB that replaces the project's. " : ". ")
                        + "Nothing has been written; pass Scl to WriteScl, then CompileSoftware.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["stationCount"] = cell.Stations.Count,
                        ["handoverCount"] = cell.Handovers().Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to generate SCL for the cell at {cellPath}");
            }
            catch (ArgumentException ex)
            {
                // The specification types refuse invalid input with ArgumentException. Reported as
                // bad parameters rather than an internal error: it is the given file that is wrong,
                // and reporting a failure would invite a retry of a typo.
                throw new McpException(
                    $"The cell at {cellPath} cannot be used: {ex.Message}", ex, McpErrorCode.InvalidParams);
            }
        }

        private static string ReadPattern(string patternDirectory, string pattern)
        {
            var path = Path.Combine(patternDirectory ?? string.Empty, pattern);

            if (!File.Exists(path))
            {
                throw new TiaMcpServer.Siemens.PortalException(
                    TiaMcpServer.Siemens.PortalErrorCode.InvalidParams,
                    $"No pattern at {path}. The cell patterns ship in spec/patterns/.");
            }

            return File.ReadAllText(path);
        }

        [McpServerTool(Name = "ListBackups"), Description("List every copy of previous state the server saved before overwriting something. A write tool takes one automatically; this is how the copy is found again. An entry with fileCount 0 is a change that was refused or failed before exporting, so there is nothing in it.")]
        public static ResponseBackups ListBackups()
        {
            try
            {
                var backups = Backups.List();
                var items = backups.Select(ToResponse).ToList();
                var empty = backups.Count(backup => backup.IsEmpty);

                return new ResponseBackups(items)
                {
                    Message = items.Count == 0
                        ? "No backups have been taken yet."
                        : $"{items.Count} backup(s), newest first; {empty} hold nothing.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = items.Count,
                        ["emptyCount"] = empty
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to list the backups");
            }
        }

        private static ResponseBackup ToResponse(Governance.BackupRecord backup)
        {
            return new ResponseBackup(
                backup.Path,
                backup.Tool,
                backup.Target,
                backup.TakenAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                backup.FileCount);
        }

        [McpServerTool(Name = "ExportSourceSnapshot"), Description("Export a PLC program to plain text (SCL/DB/AWL sources, UDTs and tag tables) laid out for version control. Blocks written in LAD, FBD or GRAPH have no text form and are reported as unsupported instead of being exported.")]
        public static ResponseExportSnapshot ExportSourceSnapshot(
            [Description("softwarePath: full path in the project structure to the plc software, e.g. 'Group1/PLC_1'")] string softwarePath,
            [Description("targetDirectory: root directory of the snapshot; blocks, types and tags are written into subfolders")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var snapshot = Portal.ExportSourceSnapshot(softwarePath, targetDirectory);

                return new ResponseExportSnapshot(snapshot.Exported, snapshot.Inconsistent, snapshot.Unsupported, snapshot.Failed)
                {
                    Message = BuildSnapshotMessage(snapshot),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["isComplete"] = snapshot.IsComplete
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to export a snapshot of '{softwarePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting a snapshot of '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static string BuildSnapshotMessage(TiaMcpServer.Siemens.SnapshotResult snapshot)
        {
            var message = $"Snapshot written: {snapshot.Exported.Count} file(s)";

            // Say out loud when the snapshot is partial. A caller that assumes otherwise would
            // treat it as a backup of the whole program, which it is not.
            if (snapshot.Unsupported.Count > 0)
            {
                message += $"; {snapshot.Unsupported.Count} block(s) have no text representation and were left out";
            }

            if (snapshot.Inconsistent.Count > 0)
            {
                message += $"; {snapshot.Inconsistent.Count} inconsistent item(s) skipped, compile the software and export again";
            }

            if (snapshot.Failed.Count > 0)
            {
                message += $"; {snapshot.Failed.Count} item(s) failed";
            }

            return message;
        }

        private static List<ResponseBlockInfo> DescribeBlocks(IEnumerable<PlcBlock>? blocks)
        {
            var described = new List<ResponseBlockInfo>();

            if (blocks == null)
            {
                return described;
            }

            foreach (var block in blocks)
            {
                if (block == null)
                {
                    continue;
                }

                described.Add(new ResponseBlockInfo
                {
                    Name = block.Name,
                    TypeName = block.GetType().Name,
                    Namespace = block.Namespace,
                    ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                    MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                    IsConsistent = block.IsConsistent,
                    HeaderName = block.HeaderName,
                    ModifiedDate = block.ModifiedDate,
                    IsKnowHowProtected = block.IsKnowHowProtected,
                    Attributes = Helper.GetAttributeList(block),
                    Description = block.ToString()
                });
            }

            return described;
        }

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

        private static ResponseSaveAsProject SaveProjectAs(string newProjectPath)
        {
            if (!Portal.SaveAsProject(newProjectPath))
            {
                throw new McpException($"Failed saving local project as '{newProjectPath}'", McpErrorCode.InternalError);
            }

            return new ResponseSaveAsProject
            {
                Message = $"Local project saved as '{newProjectPath}'",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }

        #endregion

        #region devices

        [McpServerTool(Name = "GetProjectTree"), Description("Get project structure as a tree view on current local project/session")]
        public static ResponseProjectTree GetProjectTree()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var tree = Portal.GetProjectTree();

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseProjectTree
                    {
                        Message = "Project tree retrieved",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed retrieving project tree", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving project tree: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDeviceInfo"), Description("Get info from a device from the current project/session")]
        public static ResponseDeviceInfo GetDeviceInfo(
            [Description("devicePath: defines the path in the project structure to the device")] string devicePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var device = Portal.GetDevice(devicePath);

                if (device != null)
                {
                    var attributes = Helper.GetAttributeList(device);

                    return new ResponseDeviceInfo
                    {
                        Message = $"Device info retrieved from '{devicePath}'",
                        Name = device.Name,
                        Attributes = attributes,
                        Description = device.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device not found at '{devicePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device info from '{devicePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDeviceItemInfo"), Description("Get info from a device item from the current project/session")]
        public static ResponseDeviceItemInfo GetDeviceItemInfo(
            [Description("deviceItemPath: defines the path in the project structure to the device item")] string deviceItemPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var deviceItem = Portal.GetDeviceItem(deviceItemPath);

                if (deviceItem != null)
                {
                    var attributes = Helper.GetAttributeList(deviceItem);

                    return new ResponseDeviceItemInfo
                    {
                        Message = $"Device item info retrieved from '{deviceItemPath}'",
                        Name = deviceItem.Name,
                        Attributes = attributes,
                        Description = deviceItem.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device item not found at '{deviceItemPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device item info from '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDevices"), Description("Get a list of all devices in the project/session")]
        public static ResponseDevices GetDevices()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetDevices();
                var responseList = new List<ResponseDeviceInfo>();

                if (list != null)
                {
                    foreach (var device in list)
                    {
                        if (device != null)
                        {
                            var attributes = Helper.GetAttributeList(device);
                            responseList.Add(new ResponseDeviceInfo
                            {
                                Name = device.Name,
                                Attributes = attributes,
                                Description = device.ToString()
                            });
                        }
                    }

                    return new ResponseDevices
                    {
                        Message = "Devices retrieved",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving devices", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving devices: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region plc software

        [McpServerTool(Name = "GetSoftwareInfo"), Description("Get plc software info")]
        public static ResponseSoftwareInfo GetSoftwareInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var software = Portal.GetPlcSoftware(softwarePath);
                if (software != null)
                {

                    var attributes = Helper.GetAttributeList(software);

                    return new ResponseSoftwareInfo
                    {
                        Message = $"Software info retrieved from '{softwarePath}'",
                        Name = software.Name,
                        Attributes = attributes,
                        Description = software.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Software not found at '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software info from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetSoftwareTree"), Description("Get the structure/tree of a given PLC software showing blocks, types, and external sources")]
        public static ResponseSoftwareTree GetSoftwareTree(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var tree = Portal.GetSoftwareTree(softwarePath);

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseSoftwareTree
                    {
                        Message = $"Software tree retrieved from '{softwarePath}'",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving software tree from '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software tree from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region blocks

        [McpServerTool(Name = "GetBlockInfo"), Description("Get a block info, which is located in the plc software")]
        public static ResponseBlockInfo GetBlockInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var block = Portal.GetBlock(softwarePath, blockPath);
                if (block != null)
                {
                    var attributes = Helper.GetAttributeList(block);

                    return new ResponseBlockInfo
                    {
                        Message = $"Block info retrieved from '{blockPath}' in '{softwarePath}'",
                        Name = block.Name,
                        TypeName = block.GetType().Name,
                        Namespace = block.Namespace,
                        ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage),block.ProgrammingLanguage),
                        MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                        IsConsistent = block.IsConsistent,
                        HeaderName = block.HeaderName,
                        ModifiedDate = block.ModifiedDate,
                        IsKnowHowProtected = block.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = block.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Block not found at '{blockPath}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving block info from '{blockPath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlocks"), Description("Get a list of blocks, which are located in plc software")]
        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetBlocks(softwarePath, regexName);

                var responseList = new List<ResponseBlockInfo>();
                foreach (var block in list)
                {
                    if (block != null)
                    {
                        var attributes = Helper.GetAttributeList(block);

                        responseList.Add(new ResponseBlockInfo
                        {
                            Name = block.Name,
                            TypeName = block.GetType().Name,
                            Namespace = block.Namespace,
                            ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                            MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                            IsConsistent = block.IsConsistent,
                            HeaderName = block.HeaderName,
                            ModifiedDate = block.ModifiedDate,
                            IsKnowHowProtected = block.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = block.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseBlocks
                    {
                        Message = $"Blocks with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving blocks with regex '{regexName}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving blocks with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlocksWithHierarchy"), Description("Get a list of all blocks with their group hierarchy from the plc software.")]
        public static ResponseBlocksWithHierarchy GetBlocksWithHierarchy(
        [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var rootGroup = Portal.GetBlockRootGroup(softwarePath);
                if (rootGroup != null)
                {
                    var hierarchy = Helper.BuildBlockHierarchy(rootGroup);
                    return new ResponseBlocksWithHierarchy
                    {
                        Message = $"Block hierarchy retrieved from '{softwarePath}'",
                        Root = hierarchy,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    // Specific failure: root group could not be resolved
                    throw new McpException($"Block root group not found for '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Generic unexpected failure wrapper
                throw new McpException($"Unexpected error retrieving block hierarchy for '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlock"), Description("Export a block from plc software to file")]
        public static ResponseExportBlock ExportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath,
            [Description("exportPath: defines the path where to export the block")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var block = Portal.ExportBlock(softwarePath, blockPath, exportPath, preservePath);
                if (block != null)
                {
                    return new ResponseExportBlock
                    {
                        Message = $"Block exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                // Should not be reachable because Portal.ExportBlock throws on failure
                throw new McpException($"Failed exporting block from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                // Map known portal errors to sharper MCP errors and messages.
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var suggestionNote = string.Empty;
                            // If the path has no '/', it may be incomplete; build suggestions using Portal's regex search and path resolver
                            if (!string.IsNullOrEmpty(blockPath) && !blockPath.Contains('/'))
                            {
                                try
                                {
                                    var escaped = Regex.Escape(blockPath);
                                    var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                                    if (blocks == null || blocks.Count == 0)
                                    {
                                        blocks = Portal.GetBlocks(softwarePath, escaped);
                                    }

                                    var candidates = blocks
                                        .Take(10)
                                        .Select(b => Portal.GetBlockPath(b))
                                        .Where(p => !string.IsNullOrWhiteSpace(p))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    if (candidates.Count > 0)
                                    {
                                        suggestionNote = $" Did you mean: {string.Join(", ", candidates)}?";
                                    }
                                }
                                catch
                                {
                                    // Best-effort suggestions only
                                }
                            }

                            var msg = $"Block not found.{suggestionNote}".Trim();
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            // Relay underlying portal error with concise reason; log full details
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export block.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";

                            Logger?.LogError(pex, "MCP ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["blockPath"], pex.Data?["exportPath"]);

                            throw new McpException(msg, McpErrorCode.InternalError);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        {
                            throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                        }
                }

                // Fallback
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting block from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocks"), Description("Export all blocks from the plc software to path")]
        public static async Task<ResponseExportBlocks> ExportBlocks(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the blocks")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks from '{softwarePath}' to '{exportPath}'");
                
                var allBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.GetBlocks(softwarePath, regexName)));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No blocks found to export",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportBlocks
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalBlocks,
                        Message = $"Starting export of {totalBlocks} blocks...",
                        progressToken
                    });
                }

                // Export blocks asynchronously
                var exportedBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.ExportBlocks(softwarePath, exportPath, regexName, preservePath)));

                // Build list of inconsistent (skipped) blocks for reporting
                var inconsistentInfos = new List<ResponseBlockInfo>();
                if (allBlocks != null)
                {
                    foreach (var b in allBlocks)
                    {
                        if (b != null && b.IsConsistent == false)
                        {
                            var attrs = Helper.GetAttributeList(b);
                            inconsistentInfos.Add(new ResponseBlockInfo
                            {
                                Name = b.Name,
                                TypeName = b.GetType().Name,
                                Namespace = b.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), b.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), b.MemoryLayout),
                                IsConsistent = b.IsConsistent,
                                HeaderName = b.HeaderName,
                                ModifiedDate = b.ModifiedDate,
                                IsKnowHowProtected = b.IsKnowHowProtected,
                                Attributes = attrs,
                                Description = b.ToString()
                            });
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedBlocks != null && progressToken != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalBlocks,
                        Message = $"Exported {exportedCount} of {totalBlocks} blocks",
                        progressToken
                    });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);

                            responseList.Add(new ResponseBlockInfo
                            {
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalBlocks,
                            Message = $"Export completed: {processedCount} blocks exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocks
                    {
                        Message = $"Export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["inconsistentBlocks"] = inconsistentInfos.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region types

        [McpServerTool(Name = "GetTypeInfo"), Description("Get a type info from the plc software")]
        public static ResponseTypeInfo GetTypeInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var type = Portal.GetType(softwarePath, typePath);
                if (type != null)
                {
                    var attributes = Helper.GetAttributeList(type);

                    return new ResponseTypeInfo
                    {
                        Message = $"Type info retrieved from '{typePath}' in '{softwarePath}'",
                        Name = type.Name,
                        TypeName = type.GetType().Name,
                        Namespace = type.Namespace,
                        IsConsistent = type.IsConsistent,
                        ModifiedDate = type.ModifiedDate,
                        IsKnowHowProtected = type.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = type.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Type not found at '{typePath}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving type info from '{typePath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetTypes"), Description("Get a list of types from the plc software")]
        public static ResponseTypes GetTypes(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetTypes(softwarePath, regexName);

                var responseList = new List<ResponseTypeInfo>();
                foreach (var type in list)
                {
                    if (type != null)
                    {
                        var attributes = Helper.GetAttributeList(type);

                        responseList.Add(new ResponseTypeInfo
                        {
                            Name = type.Name,
                            TypeName = type.GetType().Name,
                            Namespace = type.Namespace,
                            IsConsistent = type.IsConsistent,
                            ModifiedDate = type.ModifiedDate,
                            IsKnowHowProtected = type.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = type.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseTypes
                    {
                        Message = $"Types with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving user defined types with regex '{regexName}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving user defined types with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportType"), Description("Export a type from the plc software")]
        public static ResponseExportType ExportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where export the type")] string exportPath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var type = Portal.ExportType(softwarePath, typePath, exportPath, preservePath);
                if (type != null)
                {
                    return new ResponseExportType
                    {
                        Message = $"Type exported from '{typePath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting type from '{typePath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException("Type not found.", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export type.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportType failed for {SoftwarePath} {TypePath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["typePath"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting type from '{typePath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportTypes"), Description("Export types from the plc software to path")]
        public static async Task<ResponseExportTypes> ExportTypes(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the types")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                // First, get the list of types to determine total count
                Logger?.LogInformation($"Starting export of types from '{softwarePath}' to '{exportPath}'");
                
                var allTypes = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.GetTypes(softwarePath, regexName)));
                var totalTypes = allTypes?.Count ?? 0;

                if (totalTypes == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No types found to export",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportTypes
                    {
                        Message = $"No types found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseTypeInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = 0,
                            ["exportedTypes"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalTypes,
                        Message = $"Starting export of {totalTypes} types...",
                        progressToken
                    });
                }

                // Export types asynchronously
                var exportedTypes = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.ExportTypes(softwarePath, exportPath, regexName, preservePath)));

                // Build list of inconsistent (skipped) types for reporting
                var inconsistentTypeInfos = new List<ResponseTypeInfo>();
                if (allTypes != null)
                {
                    foreach (var t in allTypes)
                    {
                        if (t != null && t.IsConsistent == false)
                        {
                            var attrs = Helper.GetAttributeList(t);
                            inconsistentTypeInfos.Add(new ResponseTypeInfo
                            {
                                Name = t.Name,
                                TypeName = t.GetType().Name,
                                Namespace = t.Namespace,
                                IsConsistent = t.IsConsistent,
                                ModifiedDate = t.ModifiedDate,
                                IsKnowHowProtected = t.IsKnowHowProtected,
                                Attributes = attrs,
                                Description = t.ToString()
                            });
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedTypes != null && progressToken != null)
                {
                    var exportedCount = exportedTypes.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalTypes,
                        Message = $"Exported {exportedCount} of {totalTypes} types",
                        progressToken
                    });
                }

                if (exportedTypes != null)
                {
                    var responseList = new List<ResponseTypeInfo>();
                    var processedCount = 0;
                    
                    foreach (var type in exportedTypes)
                    {
                        if (type != null)
                        {
                            var attributes = Helper.GetAttributeList(type);

                            responseList.Add(new ResponseTypeInfo
                            {
                                Name = type.Name,
                                TypeName = type.GetType().Name,
                                Namespace = type.Namespace,
                                IsConsistent = type.IsConsistent,
                                ModifiedDate = type.ModifiedDate,
                                IsKnowHowProtected = type.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = type.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalTypes,
                            Message = $"Export completed: {processedCount} types exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Type export completed: {processedCount} types exported in {duration:F2} seconds");

                    return new ResponseExportTypes
                    {
                        Message = $"Export completed: {processedCount} types with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentTypeInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = totalTypes,
                            ["exportedTypes"] = processedCount,
                            ["inconsistentTypes"] = inconsistentTypeInfos.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Type export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting types '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region documents

        [McpServerTool(Name = "ExportAsDocuments"), Description("Export as documents (.s7dcl/.s7res) from a block in the plc software to path")]
        public static ResponseExportAsDocuments ExportAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportAsDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }
                if (Portal.ExportAsDocuments(softwarePath, blockPath, exportPath, preservePath))
                {
                    return new ResponseExportAsDocuments
                    {
                        Message = $"Documents exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting documents from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocksAsDocuments"), Description("Export as documents (.s7dcl/.s7res) from blocks in the plc software to path")]
        public static async Task<ResponseExportBlocksAsDocuments> ExportBlocksAsDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportBlocksAsDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks as documents from '{softwarePath}' to '{exportPath}'");
                
                var allBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.GetBlocks(softwarePath, regexName)));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No blocks found to export as documents",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalBlocks,
                        Message = $"Starting export of {totalBlocks} blocks as documents...",
                        progressToken
                    });
                }

                // Export blocks as documents asynchronously
                var exportedBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.ExportBlocksAsDocuments(softwarePath, exportPath, regexName, preservePath)));
                
                // Send progress update after export completion
                if (exportedBlocks != null && progressToken != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalBlocks,
                        Message = $"Exported {exportedCount} of {totalBlocks} blocks as documents",
                        progressToken
                    });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);

                            responseList.Add(new ResponseBlockInfo
                            {
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalBlocks,
                            Message = $"Document export completed: {processedCount} blocks exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Document export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"Document export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting documents to '{exportPath}'");
                throw new McpException($"Unexpected error exporting documents to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static ImportDocumentOptions ParseImportDocumentOption(string option)
        {
            if (string.IsNullOrWhiteSpace(option)) return ImportDocumentOptions.Override;

            var normalized = option.Trim();

            // Primary: accept exact enum names (case-insensitive)
            if (Enum.TryParse<ImportDocumentOptions>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            // Aliases and common misspellings
            switch (normalized.ToLowerInvariant())
            {
                case "override": return ImportDocumentOptions.Override;
                case "none": return ImportDocumentOptions.None;
                case "skipinactiveculture":
                case "skipinactivecultures":
                case "skipinactive":
                case "skipinactivecult":
                    return ImportDocumentOptions.SkipInactiveCultures;
                case "activeinactiveculture":
                case "activateinactivecultures":
                case "activeinactivecultures":
                case "activateinactive":
                    return ImportDocumentOptions.ActivateInactiveCultures;
                default:
                    throw new McpException($"Invalid importOption '{option}'. Allowed: None, Override, SkipInactiveCultures, ActivateInactiveCultures", McpErrorCode.InvalidParams);
            }
        }

        private static List<string> GetResMissingEnUsIds(string directory, string baseName)
        {
            var resPath = Path.Combine(directory, baseName + ".s7res");
            var missing = new List<string>();
            if (!File.Exists(resPath))
            {
                return missing;
            }
            var xdoc = XDocument.Load(resPath);
            XNamespace ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
            foreach (var comment in xdoc.Descendants(ns + "Comment"))
            {
                var hasEnUs = comment.Elements(ns + "MultiLanguageText")
                                     .Any(e => string.Equals((string?)e.Attribute("Lang"), "en-US", StringComparison.OrdinalIgnoreCase));
                if (!hasEnUs)
                {
                    var id = (string?)comment.Attribute("Id") ?? "";
                    missing.Add(id);
                }
            }
            return missing;
        }

        #endregion
    }
}
