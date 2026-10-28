using Siemens.Engineering.HmiUnified.HmiAlarm;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedLogging;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Finds every reason a logging list cannot be created on an HMI as it stands.</summary>
    /// <remarks>
    /// Measured 2026-10-28 on TIA Portal V20: data logs and alarm logs share one set of names, unique in any
    /// case; a logging tag's name is unique among the logging tags of its HMI tag. An alarm class already
    /// logged to an alarm log is refused rather than moved: the tool changes no class that was set before,
    /// so undoing it is setting the log back to empty, which TIA Portal takes. A class can also name a log
    /// that was deleted, since TIA Portal keeps the name; creating a log of that name would log the class
    /// to it without a word, so that is refused too.
    /// </remarks>
    internal sealed class UnifiedLoggingChecker
    {
        private readonly UnifiedHmiSoftware _hmi;
        private readonly HashSet<string> _logNames;
        private readonly HashSet<string> _dataLogs;

        /// <summary>Creates a checker for one HMI, reading its logs and alarm classes once.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        public UnifiedLoggingChecker(UnifiedHmiSoftware hmi)
        {
            _hmi = hmi;
            _dataLogs = new HashSet<string>(hmi.DataLogs.Select(log => log.Name), StringComparer.OrdinalIgnoreCase);
            _logNames = new HashSet<string>(_dataLogs.Concat(hmi.AlarmLogs.Select(log => log.Name)), StringComparer.OrdinalIgnoreCase);
            Classes = hmi.AlarmClasses.ToDictionary(found => found.Name, found => found, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The HMI's alarm classes, system ones included, by name in any case.</summary>
        public IReadOnlyDictionary<string, HmiAlarmClass> Classes { get; }

        /// <summary>Every problem the list has on this HMI, what is missing apart from what conflicts.</summary>
        /// <param name="list">The logs and logging tags.</param>
        /// <returns>Names that do not exist, and names or links that conflict; both empty when there is none.</returns>
        public (List<string> Missing, List<string> Conflicts) Problems(UnifiedLoggingList list)
        {
            var newLogs = list.DataLogs.Concat(list.AlarmLogs.Select(log => log.Name)).ToList();
            var conflicts = newLogs.Where(_logNames.Contains).Select(name => $"a data log or alarm log named '{name}' already exists").ToList();

            var shared = list.DataLogs.Intersect(list.AlarmLogs.Select(log => log.Name), StringComparer.OrdinalIgnoreCase);
            conflicts.AddRange(shared.Select(name => $"'{name}' is asked for as a data log and as an alarm log; the two share one set of names"));
            conflicts.AddRange(StrayClasses(list, newLogs));

            var missing = new List<string>();
            foreach (var log in list.AlarmLogs)
            {
                foreach (var name in log.Classes)
                {
                    AddClassProblem(name, log.Name, missing, conflicts);
                }
            }

            var dataLogs = new HashSet<string>(_dataLogs.Concat(list.DataLogs), StringComparer.OrdinalIgnoreCase);
            foreach (var logged in list.LoggingTags)
            {
                AddLoggingTagProblem(logged, dataLogs, missing, conflicts);
            }

            return (missing, conflicts);
        }

        /// <summary>Classes not in the list that still name a deleted log the list creates again.</summary>
        private IEnumerable<string> StrayClasses(UnifiedLoggingList list, IReadOnlyCollection<string> newLogs)
        {
            var listed = new HashSet<string>(list.AlarmLogs.SelectMany(log => log.Classes), StringComparer.OrdinalIgnoreCase);

            return Classes.Values
                .Where(found => !listed.Contains(found.Name) && IsDangling(found.Log) && newLogs.Contains(found.Log, StringComparer.OrdinalIgnoreCase))
                .Select(found => $"alarm class '{found.Name}' still names alarm log '{found.Log}', which was deleted; creating '{found.Log}' would log it there too, so empty its log in TIA Portal first");
        }

        private void AddClassProblem(string name, string alarmLog, List<string> missing, List<string> conflicts)
        {
            if (!Classes.TryGetValue(name, out var found))
            {
                missing.Add($"alarm log '{alarmLog}' logs alarm class '{name}', which does not exist");
                return;
            }

            if (IsDangling(found.Log))
            {
                conflicts.Add($"alarm class '{found.Name}' names alarm log '{found.Log}', which was deleted; empty its log in TIA Portal first");
                return;
            }

            if (!string.IsNullOrEmpty(found.Log))
            {
                conflicts.Add($"alarm class '{found.Name}' is already logged to '{found.Log}'; it is not moved to '{alarmLog}'");
            }
        }

        private void AddLoggingTagProblem(UnifiedLoggingTag logged, HashSet<string> dataLogs, List<string> missing, List<string> conflicts)
        {
            var tag = _hmi.Tags.Find(logged.Tag);
            if (tag == null)
            {
                missing.Add($"logging tag '{logged.Name}' logs tag '{logged.Tag}', which does not exist");
                return;
            }

            if (!dataLogs.Contains(logged.DataLog))
            {
                missing.Add($"logging tag '{logged.Name}' writes to data log '{logged.DataLog}', which does not exist");
            }

            // Openness's Find is exact; the names are compared in any case, as everywhere else in this tool.
            if (tag.LoggingTags.Any(existing => string.Equals(existing.Name, logged.Name, StringComparison.OrdinalIgnoreCase)))
            {
                conflicts.Add($"tag '{tag.Name}' already has a logging tag named '{logged.Name}'");
            }
        }

        private bool IsDangling(string? log)
        {
            return !string.IsNullOrEmpty(log) && !_logNames.Contains(log!);
        }
    }
}
