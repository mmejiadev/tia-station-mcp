using System.Collections.Generic;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>WinCC Unified data logs, alarm logs and logging tags to create, in that order.</summary>
    public sealed class UnifiedLoggingList
    {
        /// <summary>Creates a list.</summary>
        /// <param name="dataLogs">Data log names, created first so the logging tags can use them.</param>
        /// <param name="alarmLogs">Alarm logs with their classes.</param>
        /// <param name="loggingTags">Logging tags.</param>
        public UnifiedLoggingList(IReadOnlyList<string> dataLogs, IReadOnlyList<UnifiedAlarmLogDescription> alarmLogs, IReadOnlyList<UnifiedLoggingTag> loggingTags)
        {
            DataLogs = dataLogs;
            AlarmLogs = alarmLogs;
            LoggingTags = loggingTags;
        }

        /// <summary>Data log names.</summary>
        public IReadOnlyList<string> DataLogs { get; }

        /// <summary>Alarm logs.</summary>
        public IReadOnlyList<UnifiedAlarmLogDescription> AlarmLogs { get; }

        /// <summary>Logging tags.</summary>
        public IReadOnlyList<UnifiedLoggingTag> LoggingTags { get; }
    }
}
