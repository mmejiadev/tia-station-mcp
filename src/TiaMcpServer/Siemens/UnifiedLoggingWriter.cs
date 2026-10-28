using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.LoggingTags;
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
    /// <summary>Creates WinCC Unified data logs, alarm logs and logging tags, all of a list or none of it.</summary>
    /// <remarks>
    /// Measured 2026-10-28 on TIA Portal V20. A logging tag names its data log, and an alarm class its alarm
    /// log, by name: both are strings, and Openness refuses a name that is not a log. Deleting a log that a
    /// logging tag or a class still names succeeds and leaves the name pointing at nothing, so an undo
    /// first deletes the logging tags and empties the classes, and deletes the logs last. A cycle Openness
    /// does not take ("Logging Cycle is invalid", or for T100ms and T250ms on an internal tag
    /// "LoggingCycleConsistency check failed") is refused there and undone with the rest.
    /// </remarks>
    internal sealed class UnifiedLoggingWriter
    {
        private const string ClassLogsFile = "alarm-class-logs.json";

        private readonly UnifiedHmiSoftware _hmi;
        // Set by Check, which reads the classes once; Create uses them so they are not read again per class.
        private IReadOnlyDictionary<string, HmiAlarmClass> _classes = new Dictionary<string, HmiAlarmClass>();

        /// <summary>Creates a writer for one HMI.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        public UnifiedLoggingWriter(UnifiedHmiSoftware hmi)
        {
            _hmi = hmi;
        }

        /// <summary>Refuses a list that cannot be created as it stands, before anything is changed.</summary>
        /// <param name="list">The logs and logging tags.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when a tag, data log or class does not exist;
        /// <see cref="PortalErrorCode.InvalidState"/> when a name is taken or a class is logged elsewhere.
        /// </exception>
        public void Check(UnifiedLoggingList list)
        {
            var checker = new UnifiedLoggingChecker(_hmi);
            var (missing, conflicts) = checker.Problems(list);
            _classes = checker.Classes;

            if (missing.Count + conflicts.Count == 0)
            {
                return;
            }

            var code = missing.Count > 0 ? PortalErrorCode.InvalidParams : PortalErrorCode.InvalidState;
            throw new PortalException(code, $"Nothing was created: {string.Join("; ", missing.Concat(conflicts))}");
        }

        /// <summary>Creates the data logs, the alarm logs with their classes, then the logging tags; undoes all of it when one fails.</summary>
        /// <param name="list">The logs and logging tags, already passed through <see cref="Check"/>.</param>
        /// <param name="backupDirectory">Where the classes' logs are saved before any is changed.</param>
        public void Create(UnifiedLoggingList list, string backupDirectory)
        {
            SaveClassLogs(list, backupDirectory);
            var made = new List<(string Name, Action Delete)>();

            try
            {
                foreach (var name in list.DataLogs)
                {
                    UnifiedRollback.Create($"data log '{name}'", made, () => _hmi.DataLogs.Create(name), (created => created.Delete, _ => { }));
                }

                foreach (var log in list.AlarmLogs)
                {
                    UnifiedRollback.Create($"alarm log '{log.Name}'", made, () => _hmi.AlarmLogs.Create(log.Name), (created => created.Delete, _ => { }));
                    LogClasses(log, made);
                }

                foreach (var logged in list.LoggingTags)
                {
                    UnifiedRollback.Create($"logging tag '{logged.Name}' of '{logged.Tag}'", made, () => _hmi.Tags.Find(logged.Tag).LoggingTags.Create(logged.Name), (created => created.Delete, created => WriteLoggingTag(created, logged)));
                }
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw UnifiedRollback.Undo(made, failure, "logs and logging tags");
            }
        }

        /// <remarks>
        /// The only existing objects this tool changes are alarm classes, and only their log, from empty. Their
        /// previous log is saved first, as every write saves what it overwrites; the check has made sure it is
        /// empty, so the file says so for each class named.
        /// </remarks>
        private void SaveClassLogs(UnifiedLoggingList list, string backupDirectory)
        {
            var classes = list.AlarmLogs.SelectMany(log => log.Classes).ToList();
            if (classes.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this write changes alarm classes");
            }

            var saved = new JsonArray(classes.Select(name => (JsonNode)new JsonObject { ["class"] = _classes[name].Name, ["log"] = _classes[name].Log ?? string.Empty }).ToArray());
            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, ClassLogsFile), saved.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <remarks>
        /// Each class goes in the undo list as soon as it names the log, so the undo empties it before the log is
        /// deleted; a class TIA Portal refuses is named as the class, not as its log.
        /// </remarks>
        private void LogClasses(UnifiedAlarmLogDescription log, List<(string Name, Action Delete)> made)
        {
            foreach (var name in log.Classes)
            {
                var found = _classes[name];

                try
                {
                    found.Log = log.Name;
                }
                catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused logging alarm class '{found.Name}' to '{log.Name}': {failure.Message.Trim()}", null, failure);
                }

                made.Add(($"the log of alarm class '{found.Name}', to be emptied", () => found.Log = string.Empty));
            }
        }

        private void WriteLoggingTag(HmiLoggingTag target, UnifiedLoggingTag logged)
        {
            target.DataLog = _hmi.DataLogs.First(log => string.Equals(log.Name, logged.DataLog, StringComparison.OrdinalIgnoreCase)).Name;
            target.LoggingMode = logged.Mode switch
            {
                UnifiedLoggingMode.OnChange => HmiLoggingMode.OnChange,
                UnifiedLoggingMode.Cyclic => HmiLoggingMode.Cyclic,
                UnifiedLoggingMode.OnDemand => HmiLoggingMode.OnDemand,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised logging mode: {logged.Mode}")
            };

            if (logged.Cycle != null)
            {
                target.Cycle = logged.Cycle;
            }
        }
    }
}
