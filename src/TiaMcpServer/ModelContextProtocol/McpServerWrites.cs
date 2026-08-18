using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Every tool that changes anything, and nothing else.
    /// </summary>
    /// <remarks>
    /// The same class as <c>McpServer</c>, in a second file. Splitting by responsibility rather
    /// than restructuring the whole surface was deliberate: what a reader needs to be able to find
    /// is the complete list of things this server can change, and that list was previously scattered
    /// through two and a half thousand lines of read tools.
    ///
    /// **Everything here calls <c>GuardedTool.Run</c>.** That is the property the file exists to make
    /// checkable by eye: a tool in this file that does not name a <c>ChangeTarget</c> and pass it to
    /// the guard is a bug, and now it is a visible one. A new write tool belongs here, and in
    /// <c>Test16GuardedWrites</c>.
    ///
    /// A partial class rather than a separate type because the MCP SDK discovers tools by attribute
    /// on a type marked <c>[McpServerToolType]</c>, and because these methods share the private
    /// service accessors — <c>GuardedWrites</c>, <c>Backups</c>, <c>JobStore</c>, <c>Portal</c> —
    /// with the read tools. Two types would mean exposing those or duplicating them.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ApplyChange"), Description("Confirm a planned change so it runs. The plan id comes from the tool that proposed it. A plan is spent once used and expires after ten minutes, so an old confirmation cannot authorise a later write.")]
        public static ResponseMessage ApplyChange(
            [Description("planId: the code of the plan to confirm, for example 'K7M-2QX'")] string planId)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var outcome = GuardedWrites.Confirm(Governance.PlanId.Parse(planId), DateTime.UtcNow);

                return new ResponseMessage
                {
                    Message = outcome.IsApplied
                        ? $"Applied plan '{planId}': {outcome.Result}"
                        : $"Plan '{planId}' was not applied: {outcome.Detail}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = outcome.IsApplied,
                        ["outcome"] = outcome.Kind.ToString()
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to apply plan '{planId}'");
            }
        }

        [McpServerTool(Name = "CreateIoSystem"), Description("Create a PROFINET IO system on a CPU so IO devices can be attached to it. The current network layout is recorded to the backup registry first, because this rewires the project; call ListBackups to find that copy.")]
        public static ResponseMessage CreateIoSystem(
            [Description("controllerPath: full path to the CPU that will act as IO controller, e.g. 'PLC_0'")] string controllerPath,
            [Description("ioSystemName: a name for the IO system, e.g. 'Cell_IO'")] string ioSystemName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(controllerPath);
                var backupDirectory = Backups.Allocate("CreateIoSystem", target);
                var request = new Governance.ChangeRequest("CreateIoSystem", target, ioSystemName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var subnet = Portal.CreateIoSystem(controllerPath, ioSystemName, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"IO system '{ioSystemName}' created on '{controllerPath}', subnet '{subnet}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create IO system '{ioSystemName}'");
            }
        }

        [McpServerTool(Name = "AssignDeviceToIoSystem"), Description("Attach an IO device to an existing PROFINET IO system. The current network layout is recorded to the backup registry first. The CPU that owns the IO system cannot be attached to it: a controller is not one of its own devices.")]
        public static ResponseMessage AssignDeviceToIoSystem(
            [Description("devicePath: full path to the IO device to attach")] string devicePath,
            [Description("ioSystemName: the IO system to attach it to")] string ioSystemName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("AssignDeviceToIoSystem", target);
                var request = new Governance.ChangeRequest("AssignDeviceToIoSystem", target, ioSystemName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Portal.AssignDeviceToIoSystem(devicePath, ioSystemName, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{devicePath}' attached to IO system '{ioSystemName}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to attach '{devicePath}' to IO system '{ioSystemName}'");
            }
        }

        [McpServerTool(Name = "CreateSimulationInstance"), Description("Create a PLCSIM Advanced virtual controller and give it an address. The address must match the CPU's address in the project, otherwise TIA Portal cannot download to it.")]
        public static ResponseSimulationInstance CreateSimulationInstance(
            [Description("instanceName: a name for the virtual controller, unique within the runtime")] string instanceName,
            [Description("ipAddress: the address to assign, matching the CPU in the project, e.g. '192.168.0.1'")] string ipAddress,
            [Description("subnetMask: usually '255.255.255.0'")] string subnetMask = "255.255.255.0")
        {
            try
            {
                var request = new Governance.ChangeRequest("CreateSimulationInstance", ChangeTarget.Simulation(instanceName), ipAddress);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var runtime = Simulation;

                        runtime.CreateInstance(instanceName);

                        return Describe(runtime.SetInstanceAddress(instanceName, ipAddress, subnetMask), $"Instance '{instanceName}' created at {ipAddress}");
                    },
                    () => new ResponseSimulationInstance(instanceName, string.Empty, string.Empty, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "StartSimulationInstance"), Description("Put a virtual controller into RUN. It must have a program: a controller that has never been downloaded to cannot start.")]
        public static ResponseSimulationInstance StartSimulationInstance(
            [Description("instanceName: the virtual controller to start")] string instanceName)
        {
            try
            {
                var request = new Governance.ChangeRequest("StartSimulationInstance", ChangeTarget.Simulation(instanceName), "Run");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var started = Simulation.StartInstance(instanceName);

                        return Describe(started, $"Instance '{instanceName}' is {started.OperatingState}");
                    },
                    () => new ResponseSimulationInstance(instanceName, string.Empty, string.Empty, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to start simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "StopSimulationInstance"), Description("Put a virtual controller into STOP.")]
        public static ResponseSimulationInstance StopSimulationInstance(
            [Description("instanceName: the virtual controller to stop")] string instanceName)
        {
            try
            {
                var request = new Governance.ChangeRequest("StopSimulationInstance", ChangeTarget.Simulation(instanceName), "Stop");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var stopped = Simulation.StopInstance(instanceName);

                        return Describe(stopped, $"Instance '{instanceName}' is {stopped.OperatingState}");
                    },
                    () => new ResponseSimulationInstance(instanceName, string.Empty, string.Empty, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to stop simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "DeleteSimulationInstance"), Description("Power off a virtual controller and remove it from the runtime.")]
        public static ResponseMessage DeleteSimulationInstance(
            [Description("instanceName: the virtual controller to remove")] string instanceName)
        {
            try
            {
                var request = new Governance.ChangeRequest("DeleteSimulationInstance", ChangeTarget.Simulation(instanceName));

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Simulation.DeleteInstance(instanceName);

                        return new ResponseMessage
                        {
                            Message = $"Instance '{instanceName}' removed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to remove simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "DownloadToSimulation"), Description("Download hardware and software to a PLCSIM Advanced virtual controller. There is no equivalent for physical hardware, by design. Compile first, and make sure an instance exists at the CPU's address. Pass runAsJob true to get a job id back immediately instead of waiting, then poll GetJobStatus.")]
        public static ResponseCompileSoftware DownloadToSimulation(
            [Description("softwarePath: full path to the CPU in the project, e.g. 'PLC_0'")] string softwarePath,
            [Description("runAsJob: return a job id at once and download in the background, default: wait for the result")] bool runAsJob = false)
        {
            try
            {
                if (runAsJob)
                {
                    return StartAsJob(
                        "DownloadToSimulation",
                        softwarePath,
                        () => DownloadToSimulation(softwarePath),
                        () => new ResponseCompileSoftware(0, 0, Array.Empty<string>()));
                }

                // The gate goes after the hand-off, not before it: a job is meant to be started
                // without waiting, and taking it here would queue behind the job already running.
                using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

                var request = new Governance.ChangeRequest("DownloadToSimulation", ChangeTarget.Program(softwarePath), "Hardware | Software");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var report = Portal.DownloadToSimulation(softwarePath);

                        return new ResponseCompileSoftware(
                            report.ErrorCount,
                            report.WarningCount,
                            report.Errors.Select(error => error.ToString()).ToList())
                        {
                            Message = report.IsSuccessful
                                ? $"'{softwarePath}' downloaded to simulation"
                                : $"Download of '{softwarePath}' reported {report.ErrorCount} error(s); see Messages",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = report.IsSuccessful
                            }
                        };
                    },
                    () => new ResponseCompileSoftware(0, 0, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to download '{softwarePath}' to simulation");
            }
        }

        [McpServerTool(Name = "WriteScl"), Description("Write SCL source into a PLC program, generating the blocks it declares. The existing blocks are exported to the backup registry first, because generation overwrites blocks of the same name; call ListBackups to find that copy. Compile afterwards to find out whether the code is valid.")]
        public static ResponseWriteScl WriteScl(
            [Description("softwarePath: full path in the project structure to the plc software, e.g. 'Group1/PLC_1'")] string softwarePath,
            [Description("sclCode: the SCL source text; it may declare more than one block")] string sclCode)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("WriteScl", target);
                var request = new Governance.ChangeRequest("WriteScl", target, Summarise(sclCode))
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var generated = Portal.WriteScl(softwarePath, sclCode, backupDirectory);

                        return new ResponseWriteScl(generated)
                        {
                            Message = $"Generated {generated.Count} block(s): {string.Join(", ", generated)}. Compile the software to check them.",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseWriteScl(Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed writing SCL into '{softwarePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error writing SCL into '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        /// <summary>
        /// Hands a long operation to the job store and reports the handle rather than the result.
        /// </summary>
        /// <typeparam name="TResponse">What the tool returns when it runs to completion.</typeparam>
        /// <param name="tool">The tool being run.</param>
        /// <param name="target">What it runs against.</param>
        /// <param name="work">The tool called for real, synchronously, on a worker thread.</param>
        /// <param name="empty">Builds the payload-free response the caller gets meanwhile.</param>
        /// <returns>The tool's own response type, empty, carrying the job id in its metadata.</returns>
        /// <remarks>
        /// **The job runs the whole tool, guard included.** It does not reach inside and start the
        /// Openness call directly, which matters for more than tidiness: the audit trail records a
        /// change as applied when the work returns, so a job that started the work and let the guard
        /// finish early would write a line claiming a compile had happened before it had.
        ///
        /// **The typed payload is lost until the job finishes**, exactly as it is for a change
        /// awaiting confirmation, and for the same reason: there is no result yet. The shape is kept
        /// so the caller does not have to handle two return types for one tool, and the metadata says
        /// plainly that this is a handle — <c>outcome</c> is <c>Running</c> and <c>jobId</c> is set.
        /// A caller that ignores both and reads <c>ErrorCount</c> sees zero, which is why
        /// <c>isFinished</c> is there to be checked first.
        ///
        /// **A job whose tool the guard stopped never reports success.** Returning without throwing
        /// is not the same as having done the work: a refusal and a change awaiting confirmation are
        /// both ordinary responses, so without <see cref="RequireApplied"/> the job would have gone
        /// to <c>Succeeded</c> while nothing was compiled or downloaded. In the default build only
        /// the refusal path can be reached, because Workshop Mode is compiled out — which is exactly
        /// why this has to be right now rather than when a machine is attached.
        /// </remarks>
        private static TResponse StartAsJob<TResponse>(
            string tool,
            string target,
            Func<TResponse> work,
            Func<TResponse> empty)
            where TResponse : ResponseMessage
        {
            var jobId = JobStore.Start(tool, target, () => RequireApplied(tool, work()));
            var response = empty();

            response.Message =
                $"'{tool}' on '{target}' accepted as job '{jobId}'. " +
                "Poll GetJobStatus for the result; nothing has been reported yet.";
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = true,
                [GuardedTool.OutcomeKey] = Jobs.JobState.Running.ToString(),
                ["jobId"] = jobId.Value,
                ["isFinished"] = false
            };

            return response;
        }

        /// <summary>
        /// Turns a change the guard stopped into a failure, so the job cannot report success for it.
        /// </summary>
        /// <typeparam name="TResponse">The tool's response type.</typeparam>
        /// <param name="tool">The tool that was run.</param>
        /// <param name="response">What it returned.</param>
        /// <returns>The response's message, when the change actually ran.</returns>
        /// <remarks>
        /// The guard writes <see cref="GuardedTool.OutcomeKey"/> only when the change did **not**
        /// run, so its presence is the signal. Throwing is right here and would be wrong one layer
        /// up: a refusal reported to a caller must stay an ordinary response, but a *job* has only
        /// its state to speak with, and <c>Succeeded</c> would say the work happened.
        /// </remarks>
        /// <exception cref="PortalException">The guard refused the change or is holding it.</exception>
        private static string RequireApplied<TResponse>(string tool, TResponse? response)
            where TResponse : ResponseMessage
        {
            var outcome = response?.Meta?[GuardedTool.OutcomeKey]?.GetValue<string>();

            if (!string.IsNullOrEmpty(outcome))
            {
                throw new TiaMcpServer.Siemens.PortalException(
                    TiaMcpServer.Siemens.PortalErrorCode.InvalidState,
                    $"'{tool}' did not run ({outcome}): {response?.Message}");
            }

            return response?.Message ?? string.Empty;
        }

        /// <summary>
        /// Shortens a value so the audit trail stays readable.
        /// </summary>
        /// <remarks>
        /// A whole SCL source would drown every other line of the trail, and the trail's job is to
        /// be read. The first line names the block being written, which is what someone scanning it
        /// is looking for; the source itself is in the backup the same change took.
        /// </remarks>
        private static string Summarise(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var firstLine = value.Split('\n')[0].Trim();

            return firstLine.Length <= SummaryLength ? firstLine : firstLine.Substring(0, SummaryLength) + "...";
        }

        [McpServerTool(Name = "SaveProject"), Description("Save the current TIA-Portal local project/session")]
        public static ResponseSaveProject SaveProject()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("SaveProject", ChangeTarget.Project);

                return GuardedTool.Run(GuardedWrites, request, SaveOpenProject, () => new ResponseSaveProject());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SaveAsProject"), Description("Save current TIA-Portal project/session with a new name")]
        public static ResponseSaveAsProject SaveAsProject(
            [Description("newProjectPath: defines the new path where to save the project")] string newProjectPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Refused before a plan is made: asking for a copy of something that cannot be
                // copied is a mistake in the call, not a change anyone needs to approve or audit.
                if (Portal.IsLocalSession)
                {
                    throw new McpException($"Cannot save local session as '{newProjectPath}'", McpErrorCode.InvalidParams);
                }

                var request = new Governance.ChangeRequest("SaveAsProject", ChangeTarget.Project, newProjectPath);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => SaveProjectAs(newProjectPath),
                    () => new ResponseSaveAsProject());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session as '{newProjectPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CloseProject"), Description("Close the current TIA-Portal project/session")]
        public static ResponseCloseProject CloseProject()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("CloseProject", ChangeTarget.Project);

                return GuardedTool.Run(GuardedWrites, request, CloseOpenProject, () => new ResponseCloseProject());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error closing local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static ResponseSaveProject SaveOpenProject()
        {
            var isSession = Portal.IsLocalSession;
            var what = isSession ? "Local session" : "Local project";
            var saved = isSession ? Portal.SaveSession() : Portal.SaveProject();

            if (!saved)
            {
                throw new McpException($"Failed to save {what.ToLowerInvariant()}", McpErrorCode.InternalError);
            }

            return new ResponseSaveProject
            {
                Message = $"{what} saved",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }

        private static ResponseCloseProject CloseOpenProject()
        {
            // Read before closing: whether this was a session is no longer answerable afterwards.
            var isSession = Portal.IsLocalSession;
            var what = isSession ? "Local session" : "Local project";
            var closed = isSession ? Portal.CloseSession() : Portal.CloseProject();

            if (!closed)
            {
                throw new McpException($"Failed closing {what.ToLowerInvariant()}", McpErrorCode.InternalError);
            }

            return new ResponseCloseProject
            {
                Message = $"{what} closed",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }

        [McpServerTool(Name = "CompileSoftware"), Description("Compile the plc software. Pass runAsJob true to get a job id back immediately instead of waiting, then poll GetJobStatus.")]
        public static ResponseCompileSoftware CompileSoftware(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("password: the password to access adminsitration, default: no password")] string password = "",
            [Description("runAsJob: return a job id at once and compile in the background, default: wait for the result")] bool runAsJob = false)
        {
            try
            {
                if (runAsJob)
                {
                    return StartAsJob(
                        "CompileSoftware",
                        softwarePath,
                        () => CompileSoftware(softwarePath, password),
                        () => new ResponseCompileSoftware(0, 0, Array.Empty<string>()));
                }

                // The gate goes after the hand-off, not before it: a job is meant to be started
                // without waiting, and taking it here would queue behind the job already running.
                using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

                // Guarded even though a compile is the verification half of the loop rather than
                // the change: it marks blocks consistent, which is what makes them downloadable.
                // A session whose policy says nothing about a program must not be able to make
                // that program's code ready for a controller.
                var request = new Governance.ChangeRequest("CompileSoftware", ChangeTarget.Program(softwarePath), "Compile");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var report = Portal.CompileSoftware(softwarePath, password);

                        // A compile that finds errors is a successful call: the errors are the
                        // answer. Throwing here would discard them, which is what the previous
                        // version did — it interpolated the CompilerResult object, so a failed
                        // build reported nothing but a type name and the caller had no idea what
                        // to fix.
                        return new ResponseCompileSoftware(
                            report.ErrorCount,
                            report.WarningCount,
                            report.Errors.Select(error => error.ToString()).ToList())
                        {
                            Message = report.IsSuccessful
                                ? $"Software '{softwarePath}' compiled: {report.WarningCount} warning(s)"
                                : $"Software '{softwarePath}' has {report.ErrorCount} error(s) and {report.WarningCount} warning(s); see Messages",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = report.IsSuccessful
                            }
                        };
                    },
                    () => new ResponseCompileSoftware(0, 0, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed compiling software '{softwarePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error compiling software '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportBlock"), Description("Import a block file to plc software")]
        public static ResponseImportBlock ImportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the block")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the block")] string importPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("ImportBlock", ChangeTarget.Program(softwarePath, groupPath), importPath);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        if (!Portal.ImportBlock(softwarePath, groupPath, importPath))
                        {
                            throw new McpException($"Failed importing block from '{importPath}' to '{groupPath}'", McpErrorCode.InternalError);
                        }

                        return new ResponseImportBlock
                        {
                            Message = $"Block imported from '{importPath}' to '{groupPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseImportBlock());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing block from '{importPath}' to '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportType"), Description("Import a type from file into the plc software")]
        public static ResponseImportType ImportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the type")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the type")] string importPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("ImportType", ChangeTarget.Program(softwarePath, groupPath), importPath);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        if (!Portal.ImportType(softwarePath, groupPath, importPath))
                        {
                            throw new McpException($"Failed importing type from '{importPath}' to '{groupPath}'", McpErrorCode.InternalError);
                        }

                        return new ResponseImportType
                        {
                            Message = $"Type imported from '{importPath}' to '{groupPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseImportType());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing type from '{importPath}' to '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportFromDocuments"), Description("Import program block from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+)")]
        public static ResponseImportFromDocuments ImportFromDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the block should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("fileNameWithoutExtension: name of the block file without extension") ] string fileNameWithoutExtension,
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                var option = ParseImportDocumentOption(importOption);

                // Pre-check .s7res for missing en-US tags
                var warnings = new JsonArray();
                try
                {
                    var missingIds = GetResMissingEnUsIds(importPath, fileNameWithoutExtension);
                    if (missingIds != null && missingIds.Count > 0)
                    {
                        Logger?.LogWarning($".s7res for '{fileNameWithoutExtension}' missing en-US tags for {missingIds.Count} items: {string.Join(", ", missingIds)}");
                        warnings.Add(new JsonObject
                        {
                            ["name"] = fileNameWithoutExtension,
                            ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to evaluate .s7res warnings");
                }

                var request = new Governance.ChangeRequest(
                    "ImportFromDocuments",
                    ChangeTarget.Program(softwarePath, groupPath),
                    fileNameWithoutExtension);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        if (!Portal.ImportFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, option))
                        {
                            throw new McpException($"Failed importing '{fileNameWithoutExtension}' from '{importPath}'", McpErrorCode.InternalError);
                        }

                        return new ResponseImportFromDocuments
                        {
                            Message = $"Imported '{fileNameWithoutExtension}' from '{importPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["warnings"] = warnings
                            }
                        };
                    },
                    () => new ResponseImportFromDocuments());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing from documents: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportBlocksFromDocuments"), Description("Import program blocks from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+)")]
        public static async Task<ResponseImportBlocksFromDocuments> ImportBlocksFromDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the blocks should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("regexName: name or regular expression to select block files (empty for all)")] string regexName = "",
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            var startTime = DateTime.Now;

            // No context means no progress token, which is the same condition as a caller that
            // did not ask for progress: this tool reports none and does the work. It is not
            // defensive padding — RequestContext cannot be constructed without a live server, so
            // a caller with no server to notify has no context to pass either.
            var progressToken = context?.Params?.ProgressToken;

            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportBlocksFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                // Determine total by scanning .s7dcl files matching regex
                int total = 0;
                var scanWarnings = new JsonArray();
                try
                {
                    if (Directory.Exists(importPath))
                    {
                        var rx = string.IsNullOrWhiteSpace(regexName) ? null : new Regex(regexName, RegexOptions.Compiled);
                        var files = Directory.GetFiles(importPath, "*.s7dcl", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(f);
                            if (rx != null && !rx.IsMatch(name))
                                continue;
                            total++;

                            try
                            {
                                var missingIds = GetResMissingEnUsIds(importPath, name);
                                if (missingIds != null && missingIds.Count > 0)
                                {
                                    scanWarnings.Add(new JsonObject
                                    {
                                        ["name"] = name,
                                        ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { /* ignore pre-scan errors */ }

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = total,
                        Message = total > 0 ? $"Starting import of {total} blocks from documents..." : "Scanning import directory...",
                        progressToken
                    });
                }

                var option = ParseImportDocumentOption(importOption);
                var request = new Governance.ChangeRequest(
                    "ImportBlocksFromDocuments",
                    ChangeTarget.Program(softwarePath, groupPath),
                    string.IsNullOrWhiteSpace(regexName) ? importPath : regexName);

                // The guard decides on the calling thread and the import runs on a worker, so a
                // refusal costs nothing and the import that does run still keeps this method
                // responsive enough to report progress.
                var response = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var imported = Portal.ImportBlocksFromDocuments(softwarePath, groupPath, importPath, regexName, option);
                        var items = DescribeBlocks(imported);

                        return new ResponseImportBlocksFromDocuments
                        {
                            Message = $"Document import completed: {items.Count} blocks imported from '{importPath}'",
                            Items = items,
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["totalBlocks"] = total,
                                ["importedBlocks"] = items.Count,
                                ["duration"] = (DateTime.Now - startTime).TotalSeconds,
                                ["warnings"] = scanWarnings
                            }
                        };
                    },
                    () => new ResponseImportBlocksFromDocuments())));

                var processed = response.Items?.Count() ?? 0;

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = processed,
                        Total = total,
                        Message = response.Message,
                        progressToken
                    });
                }

                Logger?.LogInformation(
                    "Document import finished: {Processed} block(s) in {Duration:F2} s",
                    processed,
                    (DateTime.Now - startTime).TotalSeconds);

                return response;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document import failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch { }
                }

                Logger?.LogError(ex, $"Failed importing documents from '{importPath}'");
                throw new McpException($"Unexpected error importing documents from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
    }
}
