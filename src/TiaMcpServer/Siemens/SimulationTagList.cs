using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>A page of a virtual controller's tag list, and how much of it was not returned.</summary>
    /// <remarks>
    /// The counts are here rather than left out because a truncated list that looks complete is the
    /// worst of the three possible answers. A caller that filtered for <c>Step</c> and got twenty
    /// entries needs to know whether that was all of them.
    /// </remarks>
    public sealed class SimulationTagList
    {
        /// <summary>Creates a page of a tag list.</summary>
        /// <param name="items">The tags returned, ordered by name.</param>
        /// <param name="matchCount">How many tags matched the filter, returned or not.</param>
        /// <param name="totalCount">How many tags the program has in total.</param>
        public SimulationTagList(IReadOnlyList<SimulationTagInfo> items, int matchCount, int totalCount)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            MatchCount = matchCount;
            TotalCount = totalCount;
        }

        /// <summary>The tags returned, ordered by name.</summary>
        public IReadOnlyList<SimulationTagInfo> Items { get; }

        /// <summary>How many tags matched the filter, whether returned or not.</summary>
        public int MatchCount { get; }

        /// <summary>
        /// How many tags the program has in total. Zero means the controller holds no program:
        /// download first, because the tag list is read from the controller and not from the project.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>Whether matching tags were left out because of the limit.</summary>
        public bool IsTruncated => Items.Count < MatchCount;
    }
}
