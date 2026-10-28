namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>How an alarm looks in one state: background, text colour and flashing; null keeps TIA Portal's.</summary>
    public sealed class UnifiedAlarmStateVisuals
    {
        /// <summary>Creates the visuals.</summary>
        /// <param name="backColor">The background as <c>R, G, B</c>, or null.</param>
        /// <param name="textColor">The text colour as <c>R, G, B</c>, or null.</param>
        /// <param name="flashing">Whether it flashes, or null.</param>
        public UnifiedAlarmStateVisuals(string? backColor, string? textColor, bool? flashing)
        {
            BackColor = backColor;
            TextColor = textColor;
            Flashing = flashing;
        }

        /// <summary>The background, or null.</summary>
        public string? BackColor { get; }

        /// <summary>The text colour, or null.</summary>
        public string? TextColor { get; }

        /// <summary>Whether it flashes, or null.</summary>
        public bool? Flashing { get; }
    }
}
