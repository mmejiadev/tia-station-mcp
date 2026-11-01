using System.Collections.Generic;

namespace TiaMcpServer.UnifiedTrends
{
    /// <summary>The trends to show in one WinCC Unified trend control, and the time it spans.</summary>
    public sealed class UnifiedTrendList
    {
        /// <summary>Creates a list.</summary>
        /// <param name="place">The screen, and the trend control on it, both existing.</param>
        /// <param name="trends">The trends, drawn in the control's first trend area in this order.</param>
        /// <param name="timeRange">How many of which unit the time axis spans, e.g. 10 and <c>Minute</c>; null to keep it.</param>
        public UnifiedTrendList((string Screen, string Control) place, IReadOnlyList<UnifiedTrendDescription> trends, (int Factor, string Base)? timeRange)
        {
            Screen = place.Screen;
            Control = place.Control;
            Trends = trends;
            TimeRange = timeRange;
        }

        /// <summary>The screen.</summary>
        public string Screen { get; }

        /// <summary>The trend control.</summary>
        public string Control { get; }

        /// <summary>The trends.</summary>
        public IReadOnlyList<UnifiedTrendDescription> Trends { get; }

        /// <summary>The time axis span, or null to keep it.</summary>
        public (int Factor, string Base)? TimeRange { get; }
    }
}
