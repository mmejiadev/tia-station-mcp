namespace TiaMcpServer.UnifiedTextLists
{
    /// <summary>One entry of a <see cref="UnifiedTextList"/>: the values it covers and the text shown for them.</summary>
    /// <remarks>
    /// An entry written by this tool covers one value (<see cref="From"/> equals <see cref="To"/>). One
    /// read back from TIA Portal may cover a range, made in the editor; it is shown as it is.
    /// </remarks>
    public sealed class UnifiedTextListEntry
    {
        /// <summary>Creates an entry.</summary>
        /// <param name="from">The first value it covers.</param>
        /// <param name="to">The last value it covers.</param>
        /// <param name="text">The text, in the project's editing language.</param>
        public UnifiedTextListEntry(long from, long to, string text)
        {
            From = from;
            To = to;
            Text = text;
        }

        /// <summary>The first value it covers.</summary>
        public long From { get; }

        /// <summary>The last value it covers.</summary>
        public long To { get; }

        /// <summary>The text.</summary>
        public string Text { get; }

        /// <summary>Whether it covers one value.</summary>
        public bool IsSingleValue => From == To;
    }
}
