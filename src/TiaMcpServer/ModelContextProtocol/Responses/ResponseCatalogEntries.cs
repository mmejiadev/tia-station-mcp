using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>What a hardware catalogue search found.</summary>
    public sealed class ResponseCatalogEntries : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="result">The search result.</param>
        public ResponseCatalogEntries(CatalogSearchResult result)
        {
            Entries = result.Entries;
            TotalFound = result.TotalFound;
            IsTruncated = result.IsTruncated;
        }

        /// <summary>The entries; copy <see cref="CatalogEntryInfo.TypeIdentifier"/> into <c>AddDevice</c>, <c>PlugModule</c> or, for a PC HMI runtime, <c>AddPcStation</c>.</summary>
        public IReadOnlyList<CatalogEntryInfo> Entries { get; }

        /// <summary>How many entries the catalogue matched.</summary>
        public int TotalFound { get; }

        /// <summary>True when more matched than are listed; narrow the filter.</summary>
        public bool IsTruncated { get; }
    }
}
