using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>Finds every reason a logging list cannot be created on an HMI as it stands.</summary>
    /// <remarks>
    /// Measured 2026-10-28 on TIA Portal V20: data logs and alarm logs share one set of names, unique in any
    /// case; a logging tag's name is unique among the logging tags of its HMI tag. An alarm class already
    /// logged to an alarm log is refused rather than moved: the tool changes no class that was set before,
    /// so undoing it is setting the log back to empty, which TIA Portal takes. Deleting a log leaves the
    /// name in the classes and logging tags that named it, so creating a log of that name would log them to
    /// it without a word; that is refused too.
    /// </remarks>
    public sealed class UnifiedLoggingRules
    {
        private readonly UnifiedLoggingState _state;
        private readonly HashSet<string> _logNames;
        private readonly HashSet<string> _tags;
        private readonly Dictionary<string, HashSet<string>> _loggingTagNames;

        /// <summary>Creates the rules for one HMI.</summary>
        /// <param name="state">What the HMI holds.</param>
        public UnifiedLoggingRules(UnifiedLoggingState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _logNames = new HashSet<string>(state.DataLogs.Concat(state.AlarmLogs), StringComparer.OrdinalIgnoreCase);
            _tags = new HashSet<string>(state.Tags, StringComparer.OrdinalIgnoreCase);
            _loggingTagNames = state.LoggingTags
                .GroupBy(existing => existing.Tag, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => new HashSet<string>(group.Select(existing => existing.Name), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Every problem the list has on this HMI, what is missing apart from what conflicts.</summary>
        /// <param name="list">The logs and logging tags.</param>
        /// <returns>Names that do not exist, and names or links that conflict; both empty when there is none.</returns>
        public (List<string> Missing, List<string> Conflicts) Problems(UnifiedLoggingList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var newLogs = new HashSet<string>(list.DataLogs.Concat(list.AlarmLogs.Select(log => log.Name)), StringComparer.OrdinalIgnoreCase);
            var conflicts = newLogs.Where(_logNames.Contains).Select(name => $"a data log or alarm log named '{name}' already exists").ToList();
            conflicts.AddRange(SharedLogNames(list));
            conflicts.AddRange(StrayClasses(list, newLogs));
            conflicts.AddRange(StrayLoggingTags(list));

            var missing = new List<string>();
            foreach (var (log, name) in list.AlarmLogs.SelectMany(log => log.Classes.Select(name => (log.Name, name))))
            {
                AddClassProblem(name, log, missing, conflicts);
            }

            var dataLogs = new HashSet<string>(_state.DataLogs.Concat(list.DataLogs), StringComparer.OrdinalIgnoreCase);
            foreach (var logged in list.LoggingTags)
            {
                AddLoggingTagProblem(logged, dataLogs, missing, conflicts);
            }

            return (missing, conflicts);
        }

        /// <remarks>The JSON reader refuses this first; a list built in code meets it here.</remarks>
        private static IEnumerable<string> SharedLogNames(UnifiedLoggingList list)
        {
            return list.DataLogs
                .Intersect(list.AlarmLogs.Select(log => log.Name), StringComparer.OrdinalIgnoreCase)
                .Select(name => $"'{name}' is asked for as a data log and as an alarm log; the two share one set of names");
        }

        /// <summary>Classes not in the list that still name a deleted log the list creates again.</summary>
        private IEnumerable<string> StrayClasses(UnifiedLoggingList list, HashSet<string> newLogs)
        {
            var listed = new HashSet<string>(list.AlarmLogs.SelectMany(log => log.Classes), StringComparer.OrdinalIgnoreCase);

            return _state.ClassLogs
                .Where(found => !listed.Contains(found.Key) && IsDangling(found.Value) && newLogs.Contains(found.Value))
                .Select(found => $"alarm class '{found.Key}' still names alarm log '{found.Value}', which was deleted; creating '{found.Value}' would log it there too, so empty its log in TIA Portal first");
        }

        /// <summary>Logging tags already on the HMI that still name a deleted data log the list creates again.</summary>
        private IEnumerable<string> StrayLoggingTags(UnifiedLoggingList list)
        {
            var newDataLogs = new HashSet<string>(list.DataLogs, StringComparer.OrdinalIgnoreCase);

            return _state.LoggingTags
                .Where(found => IsDangling(found.DataLog) && newDataLogs.Contains(found.DataLog))
                .Select(found => $"logging tag '{found.Name}' of tag '{found.Tag}' still names data log '{found.DataLog}', which was deleted; creating '{found.DataLog}' would log it there too, so give it another data log or delete it in TIA Portal first");
        }

        private void AddClassProblem(string name, string alarmLog, List<string> missing, List<string> conflicts)
        {
            if (!_state.ClassLogs.TryGetValue(name, out var log))
            {
                missing.Add($"alarm log '{alarmLog}' logs alarm class '{name}', which does not exist");
                return;
            }

            if (IsDangling(log))
            {
                conflicts.Add($"alarm class '{name}' names alarm log '{log}', which was deleted; empty its log in TIA Portal first");
                return;
            }

            if (!string.IsNullOrEmpty(log))
            {
                conflicts.Add($"alarm class '{name}' is already logged to '{log}'; it is not moved to '{alarmLog}'");
            }
        }

        private void AddLoggingTagProblem(UnifiedLoggingTag logged, HashSet<string> dataLogs, List<string> missing, List<string> conflicts)
        {
            if (!_tags.Contains(logged.Tag))
            {
                missing.Add($"logging tag '{logged.Name}' logs tag '{logged.Tag}', which does not exist");
                return;
            }

            if (!dataLogs.Contains(logged.DataLog))
            {
                missing.Add($"logging tag '{logged.Name}' writes to data log '{logged.DataLog}', which does not exist");
            }

            if (_loggingTagNames.TryGetValue(logged.Tag, out var names) && names.Contains(logged.Name))
            {
                conflicts.Add($"tag '{logged.Tag}' already has a logging tag named '{logged.Name}'");
            }
        }

        private bool IsDangling(string? log)
        {
            return !string.IsNullOrEmpty(log) && !_logNames.Contains(log!);
        }
    }
}
