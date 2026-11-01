using System.Drawing;

namespace TiaMcpServer.UnifiedTrends
{
    /// <summary>One trend to show in a WinCC Unified trend control: what it draws, and how.</summary>
    public sealed class UnifiedTrendDescription
    {
        /// <summary>Creates a trend.</summary>
        /// <param name="source">The HMI tag, and the logging tag of it to read the logged values from, or null for live values only.</param>
        /// <param name="name">The name the legend shows, or null to keep TIA Portal's.</param>
        /// <param name="look">The line colour and the trend mode (<c>Points</c>, <c>Interpolated</c>, ...), each null to keep TIA Portal's.</param>
        public UnifiedTrendDescription((string Tag, string? LoggingTag) source, string? name, (Color? Color, string? Mode) look)
        {
            Tag = source.Tag;
            LoggingTag = source.LoggingTag;
            Name = name;
            Color = look.Color;
            Mode = look.Mode;
        }

        /// <summary>The HMI tag drawn.</summary>
        public string Tag { get; }

        /// <summary>The logging tag of <see cref="Tag"/> whose logged values are drawn, or null for live values only.</summary>
        public string? LoggingTag { get; }

        /// <summary>The name the legend shows, or null to keep TIA Portal's.</summary>
        public string? Name { get; }

        /// <summary>The line colour, or null to keep TIA Portal's.</summary>
        public Color? Color { get; }

        /// <summary>The trend mode as Openness names it, e.g. <c>Stepped</c>, or null to keep TIA Portal's (<c>Interpolated</c>).</summary>
        public string? Mode { get; }
    }
}
