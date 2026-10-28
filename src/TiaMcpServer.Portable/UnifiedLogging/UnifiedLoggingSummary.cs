using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedAlarms;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>The change plan's line for a logging list: every log and logging tag, with what it logs.</summary>
    /// <remarks>Creating logs takes no backup, so the plan is the only record of what was asked for.</remarks>
    public static class UnifiedLoggingSummary
    {
        /// <summary>Describes a list.</summary>
        /// <param name="list">The list.</param>
        /// <returns>E.g. <c>data log Log_Tolva; alarm log Alarmes (Alarm, Warning); logging tag Nivell_log (Nivell to Log_Tolva, cyclic T1s)</c>.</returns>
        public static string Of(UnifiedLoggingList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var parts = new List<string>();
            parts.AddRange(list.DataLogs.Select(name => $"data log {name}"));
            parts.AddRange(list.AlarmLogs.Select(log => $"alarm log {log.Name} ({string.Join(", ", log.Classes)})"));
            parts.AddRange(list.LoggingTags.Select(Describe));

            return string.Join("; ", parts);
        }

        private static string Describe(UnifiedLoggingTag logged)
        {
            var timing = logged.Cycle == null ? UnifiedAlarmJson.Camel(logged.Mode.ToString()) : $"{UnifiedAlarmJson.Camel(logged.Mode.ToString())} {logged.Cycle}";

            return $"logging tag {logged.Name} ({logged.Tag} to {logged.DataLog}, {timing})";
        }
    }
}
