using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>A page of a virtual controller's tag list.</summary>
    public sealed class ResponseSimulationTags : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">The tags returned, ordered by name.</param>
        /// <param name="matchCount">How many tags matched the filter, returned or not.</param>
        /// <param name="totalCount">How many tags the program has in total.</param>
        public ResponseSimulationTags(IReadOnlyList<ResponseSimulationTag> items, int matchCount, int totalCount)
        {
            Items = items;
            MatchCount = matchCount;
            TotalCount = totalCount;
        }

        /// <summary>The tags returned, ordered by name.</summary>
        public IReadOnlyList<ResponseSimulationTag> Items { get; }

        /// <summary>How many tags matched the filter, whether returned or not.</summary>
        public int MatchCount { get; }

        /// <summary>
        /// How many tags the program has in total. Zero means the controller holds no program at
        /// all: the tag list is read from the controller, so download before expecting names.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// Whether matching tags were left out because of the limit. Reported so a filtered list
        /// that happens to be a page is not mistaken for the whole answer.
        /// </summary>
        public bool IsTruncated => Items.Count < MatchCount;
    }
}
