using Siemens.Engineering.HmiUnified.HmiLogging.HmiLoggingCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedLogging;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Changes WinCC Unified log settings, all of a list or none of it.</summary>
    /// <remarks>
    /// Every log's settings are read and saved before any is written; a write TIA Portal refuses takes every
    /// log already touched back to what it had, by the same ordered plan, and says what could not be.
    /// </remarks>
    internal sealed class UnifiedLogSettingsWriter
    {
        private const string BackupFile = "log-settings.json";

        private readonly List<(LoggingBase Log, UnifiedLogSettings Before, UnifiedLogSettings After)> _changes;

        /// <summary>Finds every log and works out what it would have, refusing a list that cannot be applied as it stands.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="requests">The settings asked.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when a log does not exist or would end with settings TIA Portal refuses.
        /// </exception>
        public UnifiedLogSettingsWriter(UnifiedHmiSoftware hmi, IReadOnlyList<UnifiedLogSettingsRequest> requests)
        {
            _changes = requests.Select(request =>
            {
                var log = Find(hmi, request.Name);
                var before = UnifiedLogSettingsAccess.Read(log);
                var after = request.Over(before);
                UnifiedLogSettingsPlan.Check(log.Name, after);

                return (log, before, after);
            }).ToList();
        }

        /// <summary>Saves every log's settings, then writes the new ones; puts them back when one is refused.</summary>
        /// <param name="backupDirectory">Where the settings before are saved.</param>
        /// <returns>Each log's name and the settings it ends with.</returns>
        public IReadOnlyList<(string Name, UnifiedLogSettings Settings)> Apply(string backupDirectory)
        {
            SaveBefore(backupDirectory);
            var touched = new List<(LoggingBase Log, UnifiedLogSettings Before, UnifiedLogSettings After)>();

            try
            {
                foreach (var change in _changes)
                {
                    touched.Add(change);
                    WriteOrExplain(change.Log, UnifiedLogSettingsPlan.Steps(change.Before, change.After));
                }
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw PutBack(touched, failure);
            }

            return _changes.Select(change => (change.Log.Name, UnifiedLogSettingsAccess.Read(change.Log))).ToList();
        }

        private static LoggingBase Find(UnifiedHmiSoftware hmi, string name)
        {
            return (LoggingBase?)hmi.DataLogs.Find(name)
                ?? hmi.AlarmLogs.Find(name)
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"There is no data log or alarm log '{name}'. Logs: {string.Join(", ", hmi.DataLogs.Select(log => log.Name).Concat(hmi.AlarmLogs.Select(log => log.Name)))}; CreateUnifiedLogging makes one");
        }

        private static void WriteOrExplain(LoggingBase log, IReadOnlyList<(UnifiedLogField Field, object Value)> steps)
        {
            foreach (var step in steps)
            {
                try
                {
                    UnifiedLogSettingsAccess.Write(log, step);
                }
                catch (Exception failure) when (!(failure is PortalException) && !UnifiedRollback.IsSessionEnded(failure))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused {step.Field} = {step.Value} on log '{log.Name}': {failure.Message.Trim()}", null, failure);
                }
            }
        }

        /// <remarks>Each log goes back from what it has now, read again, as the failed write may have left it half-way.</remarks>
        private static PortalException PutBack(List<(LoggingBase Log, UnifiedLogSettings Before, UnifiedLogSettings After)> touched, Exception failure)
        {
            var left = Enumerable.Reverse(touched)
                .Select(change => TryRestore(change.Log, change.Before))
                .Where(problem => problem != null)
                .ToList();
            var outcome = left.Count == 0
                ? "Nothing was changed: every log touched has its settings as before."
                : $"Putting the settings back failed for {string.Join("; ", left)}. They are in the backup registry; set them by hand in TIA Portal.";

            return new PortalException((failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed, $"{failure.Message.Trim()} {outcome}", null, failure);
        }

        /// <returns>Null when the log has its settings back; otherwise the log and why not, for the caller to report.</returns>
        private static string? TryRestore(LoggingBase log, UnifiedLogSettings before)
        {
            try
            {
                WriteOrExplain(log, UnifiedLogSettingsPlan.Steps(UnifiedLogSettingsAccess.Read(log), before));
                return null;
            }
            catch (Exception restoreFailure) when (!UnifiedRollback.IsSessionEnded(restoreFailure))
            {
                return $"'{log.Name}' ({restoreFailure.Message.Trim()})";
            }
        }

        private void SaveBefore(string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the logs' settings are saved before any changes");
            }

            var before = new JsonArray(_changes.Select(change =>
            {
                var described = UnifiedLogSettingsAccess.DescribeExactly(change.Before);
                described["name"] = change.Log.Name;
                return (JsonNode)described;
            }).ToArray());

            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, BackupFile), before.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
