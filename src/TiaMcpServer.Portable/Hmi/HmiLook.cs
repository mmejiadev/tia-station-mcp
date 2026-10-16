namespace TiaMcpServer.Hmi
{
    /// <summary>The colours, font and alignment a screen item overrides; empty means the style's own.</summary>
    /// <remarks>
    /// The style is what TIA Portal gives a new item, measured on the class projects. The teacher's
    /// <c>principal</c> screen changes some of it — a black I/O field with magenta digits in a larger
    /// font — and an item that should look like that says so here.
    /// </remarks>
    public sealed class HmiLook
    {
        /// <summary>Creates a look.</summary>
        /// <param name="backColor">Background colour as <c>R, G, B</c>, or empty for the style's.</param>
        /// <param name="foreColor">Text colour as <c>R, G, B</c>, or empty for the style's.</param>
        /// <param name="font">Font size and style; <see cref="HmiFont.Unset"/> for the style's.</param>
        /// <param name="horizontalAlignment"><c>Left</c>, <c>Center</c> or <c>Right</c>, or empty for the style's.</param>
        public HmiLook(string backColor, string foreColor, HmiFont font, string horizontalAlignment)
        {
            BackColor = backColor ?? string.Empty;
            ForeColor = foreColor ?? string.Empty;
            Font = font ?? HmiFont.Unset;
            HorizontalAlignment = horizontalAlignment ?? string.Empty;
        }

        /// <summary>A look that sets only the background colour.</summary>
        /// <param name="backColor">Background colour as <c>R, G, B</c>.</param>
        /// <returns>The look.</returns>
        public static HmiLook Colored(string backColor)
        {
            return new HmiLook(backColor, string.Empty, HmiFont.Unset, string.Empty);
        }

        /// <summary>Background colour as <c>R, G, B</c>, or empty for the style's.</summary>
        public string BackColor { get; }

        /// <summary>Text colour as <c>R, G, B</c>, or empty for the style's.</summary>
        public string ForeColor { get; }

        /// <summary>Font size and style.</summary>
        public HmiFont Font { get; }

        /// <summary><c>Left</c>, <c>Center</c> or <c>Right</c>, or empty for the style's.</summary>
        public string HorizontalAlignment { get; }
    }
}
