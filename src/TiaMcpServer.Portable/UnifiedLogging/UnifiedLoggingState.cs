using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>What a WinCC Unified HMI already holds that a logging list can clash with, as read from TIA Portal.</summary>
    /// <remarks>Only names and links: nothing here is read that a rule does not use.</remarks>
    public sealed class UnifiedLoggingState
    {
        /// <summary>Describes what an HMI holds.</summary>
        /// <param name="logs">The names of the HMI's data logs and of its alarm logs.</param>
        /// <param name="classLogs">Every alarm class, system ones included, and the log it names; empty for none.</param>
        /// <param name="tags">The HMI tags the list logs that exist, by the name the list gives them.</param>
        /// <param name="loggingTags">
        /// Existing logging tags, each with its HMI tag and the data log it names: those of the HMI tags the
        /// list logs, and every one on the HMI when the list creates a data log.
        /// </param>
        public UnifiedLoggingState(
            (IReadOnlyCollection<string> DataLogs, IReadOnlyCollection<string> AlarmLogs) logs,
            IReadOnlyDictionary<string, string> classLogs,
            IReadOnlyCollection<string> tags,
            IReadOnlyCollection<(string Tag, string Name, string DataLog)> loggingTags)
        {
            DataLogs = logs.DataLogs ?? throw new ArgumentNullException(nameof(logs));
            AlarmLogs = logs.AlarmLogs ?? throw new ArgumentNullException(nameof(logs));
            ClassLogs = (classLogs ?? throw new ArgumentNullException(nameof(classLogs))).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
            LoggingTags = loggingTags ?? throw new ArgumentNullException(nameof(loggingTags));
        }

        /// <summary>The names of the HMI's data logs.</summary>
        public IReadOnlyCollection<string> DataLogs { get; }

        /// <summary>The names of the HMI's alarm logs.</summary>
        public IReadOnlyCollection<string> AlarmLogs { get; }

        /// <summary>Every alarm class and the log it names, by class name in any case.</summary>
        public IReadOnlyDictionary<string, string> ClassLogs { get; }

        /// <summary>The HMI tags the list logs that exist.</summary>
        public IReadOnlyCollection<string> Tags { get; }

        /// <summary>Existing logging tags, with their HMI tag and data log.</summary>
        public IReadOnlyCollection<(string Tag, string Name, string DataLog)> LoggingTags { get; }
    }
}
