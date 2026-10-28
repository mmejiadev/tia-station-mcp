using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A property that shows the entry of a text list a tag's value picks: "Running" for 1.</summary>
    /// <remarks>
    /// Openness's resource-list dynamization (measured 2026-10-25 on TIA Portal V20: on a text's
    /// <c>Text</c> it takes a tag and a list and compiles). It takes the name of a list that does not
    /// exist without a word, and only the compile reports it, so the list is checked before drawing.
    /// A symbolic IO field needs no animation: its own <c>ResourceList</c> property names the list.
    /// </remarks>
    public sealed class UnifiedListAnimation : UnifiedAnimation
    {
        /// <summary>Creates a list animation.</summary>
        /// <param name="path">The property's path, e.g. <c>Text</c>.</param>
        /// <param name="tag">The HMI tag whose value picks the entry.</param>
        /// <param name="list">The text list.</param>
        public UnifiedListAnimation(string path, string tag, string list)
            : base(path)
        {
            Tag = tag;
            List = list;
        }

        /// <summary>The HMI tag.</summary>
        public string Tag { get; }

        /// <summary>The text list.</summary>
        public string List { get; }

        /// <inheritdoc />
        public override IReadOnlyList<string> LinkedTags => new[] { Tag };
    }
}
