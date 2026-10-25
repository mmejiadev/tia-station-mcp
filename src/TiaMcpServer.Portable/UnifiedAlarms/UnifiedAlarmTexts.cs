namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>What an operator reads of an alarm: its text, an info text, where it comes from and its area.</summary>
    /// <remarks>Texts are written as TIA Portal's formatted text: a plain one is refused as "an invalid format" (measured 2026-10-25).</remarks>
    public sealed class UnifiedAlarmTexts
    {
        /// <summary>Creates the texts.</summary>
        /// <param name="text">The alarm text, in the project's editing language.</param>
        /// <param name="info">The info text, or null.</param>
        /// <param name="origin">Where the alarm comes from, e.g. a station, or null.</param>
        /// <param name="area">The plant area, or null.</param>
        public UnifiedAlarmTexts(string text, string? info, string? origin, string? area)
        {
            Text = text;
            Info = info;
            Origin = origin;
            Area = area;
        }

        /// <summary>The alarm text.</summary>
        public string Text { get; }

        /// <summary>The info text, or null.</summary>
        public string? Info { get; }

        /// <summary>The origin, or null.</summary>
        public string? Origin { get; }

        /// <summary>The area, or null.</summary>
        public string? Area { get; }
    }
}
