namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>What one faceplate interface property is given: an HMI tag, or a fixed value.</summary>
    /// <remarks>
    /// Both are written as the property's value, a string (measured 2026-10-30; a tag dynamization on an
    /// interface property is "not supported"). An interface property says nothing of its own type, so a
    /// tag is marked as one by the caller, and only a tag is checked to exist before anything is placed.
    /// </remarks>
    public sealed class UnifiedFaceplateInterfaceValue
    {
        private UnifiedFaceplateInterfaceValue(string value, bool isTag)
        {
            Value = value;
            IsTag = isTag;
        }

        /// <summary>The tag name, or the fixed value as text.</summary>
        public string Value { get; }

        /// <summary>True when <see cref="Value"/> names an HMI tag.</summary>
        public bool IsTag { get; }

        /// <summary>An HMI tag, or a member of one: <c>M1</c>, <c>M1.Ft</c>.</summary>
        /// <param name="tag">The tag.</param>
        /// <returns>The value.</returns>
        public static UnifiedFaceplateInterfaceValue Tag(string tag)
        {
            return new UnifiedFaceplateInterfaceValue(tag, isTag: true);
        }

        /// <summary>A fixed value: a text, a number or a truth value, written as text.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The value.</returns>
        public static UnifiedFaceplateInterfaceValue Fixed(string value)
        {
            return new UnifiedFaceplateInterfaceValue(value, isTag: false);
        }

        /// <summary>The HMI tag the value names, without a member path: <c>M1</c> for <c>M1.Ft</c>.</summary>
        /// <returns>The tag's name, or null for a fixed value.</returns>
        public string? RootTag()
        {
            if (!IsTag)
            {
                return null;
            }

            var dot = Value.IndexOf('.');

            return dot < 0 ? Value : Value.Substring(0, dot);
        }
    }
}
