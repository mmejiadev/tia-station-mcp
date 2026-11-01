using System;
using System.Linq;

namespace TiaMcpServer.UnifiedTrends
{
    /// <summary>The change plan's line for a trend list: the control, every trend's source and the time range.</summary>
    public static class UnifiedTrendSummary
    {
        /// <summary>Describes a list.</summary>
        /// <param name="list">The list.</param>
        /// <returns>E.g. <c>Trend_1 on Start: Level:Level_log, Pressure; 10 Minute</c>.</returns>
        public static string Of(UnifiedTrendList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var sources = string.Join(", ", list.Trends.Select(trend => trend.LoggingTag == null ? trend.Tag : $"{trend.Tag}:{trend.LoggingTag}"));
            var range = list.TimeRange == null ? string.Empty : $"; {list.TimeRange.Value.Factor} {list.TimeRange.Value.Base}";

            return $"{list.Control} on {list.Screen}: {sources}{range}";
        }
    }
}
