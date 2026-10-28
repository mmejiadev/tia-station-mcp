namespace TiaMcpServer.UnifiedTags
{
    /// <summary>One limit of a WinCC Unified tag: a constant, or another HMI tag holding the limit.</summary>
    public sealed class UnifiedTagLimit
    {
        private UnifiedTagLimit(bool isTag, string value)
        {
            IsTag = isTag;
            Value = value;
        }

        /// <summary>Whether the limit is read from another HMI tag, named by <see cref="Value"/>.</summary>
        public bool IsTag { get; }

        /// <summary>The constant, written as TIA Portal compiles it, or the name of the tag holding the limit.</summary>
        public string Value { get; }

        /// <summary>A constant limit.</summary>
        /// <param name="value">The value as text; for an internal tag already checked against its type.</param>
        /// <returns>The limit.</returns>
        public static UnifiedTagLimit Constant(string value)
        {
            return new UnifiedTagLimit(false, value);
        }

        /// <summary>A limit held by another HMI tag.</summary>
        /// <param name="tagName">The HMI tag's name.</param>
        /// <returns>The limit.</returns>
        public static UnifiedTagLimit Tag(string tagName)
        {
            return new UnifiedTagLimit(true, tagName);
        }
    }
}
