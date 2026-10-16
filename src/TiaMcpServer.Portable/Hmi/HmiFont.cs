namespace TiaMcpServer.Hmi
{
    /// <summary>An item's font size and style; 0 and empty keep the style's own.</summary>
    /// <remarks>
    /// The family is always Tahoma, the only one in the class projects. The styles measured there are
    /// <c>Bold</c>, which every new item gets, and <c>Regular</c>.
    /// </remarks>
    public sealed class HmiFont
    {
        /// <summary>A font that overrides nothing.</summary>
        public static readonly HmiFont Unset = new HmiFont(0, string.Empty);

        /// <summary>Creates a font.</summary>
        /// <param name="size">The size, or 0 for the style's.</param>
        /// <param name="style"><c>Bold</c> or <c>Regular</c>, or empty for the style's.</param>
        public HmiFont(int size, string style)
        {
            Size = size;
            Style = style ?? string.Empty;
        }

        /// <summary>The size, or 0 for the style's.</summary>
        public int Size { get; }

        /// <summary><c>Bold</c> or <c>Regular</c>, or empty for the style's.</summary>
        public string Style { get; }
    }
}
