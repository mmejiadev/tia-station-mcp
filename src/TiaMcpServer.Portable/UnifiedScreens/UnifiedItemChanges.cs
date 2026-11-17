using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>What an edit does to a screen's items: removes some, sets properties of some in place, draws some.</summary>
    public sealed class UnifiedItemChanges
    {
        /// <summary>Creates the changes.</summary>
        /// <param name="remove">The items to delete, by name.</param>
        /// <param name="set">The items whose properties are set in place.</param>
        /// <param name="draw">The items to draw: added, or replacing one of the same name.</param>
        public UnifiedItemChanges(IReadOnlyList<string> remove, IReadOnlyList<UnifiedItemSettings> set, IReadOnlyList<UnifiedItemDescription> draw)
        {
            Remove = remove;
            Set = set;
            Draw = draw;
        }

        /// <summary>The items to delete, by name.</summary>
        public IReadOnlyList<string> Remove { get; }

        /// <summary>The items whose properties are set in place.</summary>
        public IReadOnlyList<UnifiedItemSettings> Set { get; }

        /// <summary>The items to draw.</summary>
        public IReadOnlyList<UnifiedItemDescription> Draw { get; }
    }
}
