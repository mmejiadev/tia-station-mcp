using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Compiling software and hardware, downloading to a simulated controller, and running those long operations as jobs.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CompileHardware"), Description("Compile a device's hardware configuration. Needed before DownloadToSimulation and after anything that invalidates the configuration — EnableSimulationSupport does. Downloading a stale configuration fails with 'Loading of hardware configuration failed', which names neither the cause nor the fix.")]
        public static ResponseCompileSoftware CompileHardware(
            [Description("deviceItemPath: full path to the device in the project, e.g. 'PLC_0'")] string deviceItemPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Guarded for the same reason CompileSoftware is: a compile is what makes something
                // downloadable, and a session whose policy says nothing about a device must not be
                // able to make that device's configuration ready for a controller.
                var request = new Governance.ChangeRequest("CompileHardware", ChangeTarget.Program(deviceItemPath), "Compile");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var report = Portal.CompileHardware(deviceItemPath);

                        return Describe(
                            report,
                            $"Hardware for '{deviceItemPath}' compiled: {report.WarningCount} warning(s)",
                            $"Hardware for '{deviceItemPath}' has {report.ErrorCount} error(s) and {report.WarningCount} warning(s); see Messages");
                    },
                    () => new ResponseCompileSoftware(0, 0, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed compiling the hardware of '{deviceItemPath}'");
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

                        return Describe(
                            report,
                            $"'{softwarePath}' downloaded to simulation",
                            $"Download of '{softwarePath}' reported {report.ErrorCount} error(s); see Messages");
                    },
                    () => new ResponseCompileSoftware(0, 0, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to download '{softwarePath}' to simulation");
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
        /// Turns a compilation or download report into the response those tools share.
        /// </summary>
        /// <param name="report">What the operation reported.</param>
        /// <param name="succeeded">The message when there are no errors.</param>
        /// <param name="failed">The message when there are.</param>
        /// <returns>The response, with the messages the caller has to act on.</returns>
        /// <remarks>
        /// **An operation that finds errors is a successful call: the errors are the answer.** That
        /// is the whole reason this shape exists, and the reason it is worth having once rather than
        /// three times — compiling software, compiling hardware and downloading all report the same
        /// way, and all three had their own copy of it until 2026-08-21.
        ///
        /// Throwing instead would discard the messages, which is what an earlier version did: it
        /// interpolated the result object, so a failed build reported nothing but a type name and
        /// the caller had no idea what to fix.
        /// </remarks>
        private static ResponseCompileSoftware Describe(
            TiaMcpServer.Siemens.CompilationReport report,
            string succeeded,
            string failed)
        {
            return new ResponseCompileSoftware(
                report.ErrorCount,
                report.WarningCount,
                report.Errors.Select(error => error.ToString()).ToList())
            {
                Message = report.IsSuccessful ? succeeded : failed,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = report.IsSuccessful
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

                        var response = Describe(
                            report,
                            $"Software '{softwarePath}' compiled: {report.WarningCount} warning(s)",
                            $"Software '{softwarePath}' has {report.ErrorCount} error(s) and {report.WarningCount} warning(s); see Messages");

                        response.Message = RecordCompilation(softwarePath, report, response.Message ?? string.Empty);

                        return response;
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
    }
}
