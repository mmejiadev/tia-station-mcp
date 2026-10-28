using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedLogging;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified data logs, alarm logs and logging tags: reading them, and creating new ones. See
    /// <see cref="UnifiedLoggingChecker"/> and <see cref="UnifiedLoggingWriter"/> for what was measured.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads a WinCC Unified HMI's data logs, alarm logs and logging tags.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <returns><c>{ dataLogs: [...], alarmLogs: [...], loggingTags: [...] }</c>.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI.</exception>
        public JsonObject GetUnifiedLogging(string hmiPath)
        {
            try
            {
                return UnifiedLoggingReader.Read(RequireUnifiedSoftware(hmiPath));
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedLogging", hmiPath, string.Empty);
            }
        }

        /// <summary>Creates WinCC Unified data logs, alarm logs and logging tags, all of them or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The logs and logging tags.</param>
        /// <param name="backupDirectory">Where the alarm classes' logs are saved before any is changed.</param>
        /// <returns>What was created, read back.</returns>
        /// <exception cref="PortalException">
        /// A name is taken; a tag, class or data log does not exist; a class is already logged elsewhere; or
        /// TIA Portal refused one, a cycle for instance. In every case nothing is left created.
        /// </exception>
        /// <remarks>
        /// The one existing thing changed is an alarm class's log, and only from empty; it is saved to the
        /// backup first, and the plan names every class.
        /// </remarks>
        public JsonObject CreateUnifiedLogging(string hmiPath, UnifiedLoggingList list, string backupDirectory)
        {
            _logger?.LogInformation("Creating Unified logging on {HmiPath}...", hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "A logging list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var writer = new UnifiedLoggingWriter(hmi);

                writer.Check(requested);
                writer.Create(requested, backupDirectory);

                return OnlyCreated(hmi, requested);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "CreateUnifiedLogging"), "CreateUnifiedLogging", hmiPath, string.Empty);
            }
        }

        private static JsonObject OnlyCreated(UnifiedHmiSoftware hmi, UnifiedLoggingList requested)
        {
            var logs = UnifiedLoggingReader.Read(hmi, includeLoggingTags: false);

            return new JsonObject
            {
                ["dataLogs"] = Only(logs["dataLogs"]!.AsArray(), requested.DataLogs),
                ["alarmLogs"] = Only(logs["alarmLogs"]!.AsArray(), requested.AlarmLogs.Select(log => log.Name).ToList()),
                ["loggingTags"] = UnifiedLoggingReader.ReadLoggingTags(hmi, requested.LoggingTags.Select(entry => (entry.Tag, entry.Name)))
            };
        }
    }
}
