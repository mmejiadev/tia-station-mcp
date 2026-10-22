using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>What a catalogue search found, and whether all of it is shown.</summary>
    public sealed class CatalogSearchResult
    {
        /// <summary>Creates a search result.</summary>
        /// <param name="entries">The entries shown.</param>
        /// <param name="totalFound">How many the catalogue matched, shown or not.</param>
        public CatalogSearchResult(IReadOnlyList<CatalogEntryInfo> entries, int totalFound)
        {
            Entries = entries;
            TotalFound = totalFound;
        }

        /// <summary>The entries shown, in the catalogue's order.</summary>
        public IReadOnlyList<CatalogEntryInfo> Entries { get; }

        /// <summary>How many entries the catalogue matched.</summary>
        public int TotalFound { get; }

        /// <summary>True when the catalogue matched more than is shown, so the filter should be narrowed.</summary>
        public bool IsTruncated => TotalFound > Entries.Count;
    }
}
