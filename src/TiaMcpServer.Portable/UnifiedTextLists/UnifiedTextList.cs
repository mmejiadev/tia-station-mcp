using System.Collections.Generic;

namespace TiaMcpServer.UnifiedTextLists
{
    /// <summary>A WinCC Unified text list: a text per value of a tag, which a symbolic IO field or a text shows.</summary>
    public sealed class UnifiedTextList
    {
        /// <summary>Creates a text list.</summary>
        /// <param name="name">The list's name.</param>
        /// <param name="entries">The entries, in order.</param>
        public UnifiedTextList(string name, IReadOnlyList<UnifiedTextListEntry> entries)
        {
            Name = name;
            Entries = entries;
        }

        /// <summary>The list's name.</summary>
        public string Name { get; }

        /// <summary>The entries, in order.</summary>
        public IReadOnlyList<UnifiedTextListEntry> Entries { get; }
    }
}
