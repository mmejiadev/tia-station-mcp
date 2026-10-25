namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The plain settings of a WinCC Unified tag: comment, start value, retention and text length.</summary>
    /// <remarks>
    /// Measured 2026-10-25: the comment is plain text in the editing language; a start value is taken by
    /// internal and linked tags; retention (<c>Persistent</c>) is disabled on a linked tag; the length is
    /// a WString's.
    /// </remarks>
    public sealed class UnifiedTagOptions
    {
        /// <summary>No options: TIA Portal's defaults.</summary>
        public static readonly UnifiedTagOptions None = new UnifiedTagOptions(null, null, null, null);

        /// <summary>Creates the options; null leaves TIA Portal's default.</summary>
        /// <param name="comment">The comment, in the project's editing language.</param>
        /// <param name="initialValue">The start value as text; for an internal tag already checked against its type.</param>
        /// <param name="persistent">Whether an internal tag keeps its value when the runtime restarts.</param>
        /// <param name="maxLength">The most characters an internal WString holds.</param>
        public UnifiedTagOptions(string? comment, string? initialValue, bool? persistent, uint? maxLength)
        {
            Comment = comment;
            InitialValue = initialValue;
            Persistent = persistent;
            MaxLength = maxLength;
        }

        /// <summary>The comment, or null.</summary>
        public string? Comment { get; }

        /// <summary>The start value, or null.</summary>
        public string? InitialValue { get; }

        /// <summary>Whether the value is retained, or null.</summary>
        public bool? Persistent { get; }

        /// <summary>A WString's length, or null.</summary>
        public uint? MaxLength { get; }
    }
}
