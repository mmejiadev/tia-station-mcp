namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The substitute value of a linked WinCC Unified tag, and when it is used.</summary>
    /// <remarks>Measured 2026-10-25: disabled on an internal tag, settable on a linked one.</remarks>
    public sealed class UnifiedTagSubstitute
    {
        /// <summary>Creates a substitute value.</summary>
        /// <param name="usage">When it is used.</param>
        /// <param name="value">The value as text, checked against the tag's type once TIA Portal has filled it in.</param>
        public UnifiedTagSubstitute(UnifiedSubstituteUsage usage, string value)
        {
            Usage = usage;
            Value = value;
        }

        /// <summary>When the substitute value is used.</summary>
        public UnifiedSubstituteUsage Usage { get; }

        /// <summary>The substitute value.</summary>
        public string Value { get; }
    }
}
