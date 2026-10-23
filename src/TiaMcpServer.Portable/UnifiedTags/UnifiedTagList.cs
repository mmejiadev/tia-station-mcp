using System.Collections.Generic;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>WinCC Unified HMI tags to create, and the tag tables to create first for them.</summary>
    public sealed class UnifiedTagList
    {
        /// <summary>Creates a list.</summary>
        /// <param name="newTables">Tag tables to create before the tags; each must not exist yet.</param>
        /// <param name="tags">The tags, in the order they are created.</param>
        public UnifiedTagList(IReadOnlyList<string> newTables, IReadOnlyList<UnifiedTagDescription> tags)
        {
            NewTables = newTables;
            Tags = tags;
        }

        /// <summary>Tag tables to create before the tags.</summary>
        public IReadOnlyList<string> NewTables { get; }

        /// <summary>The tags, in the order they are created.</summary>
        public IReadOnlyList<UnifiedTagDescription> Tags { get; }
    }
}
