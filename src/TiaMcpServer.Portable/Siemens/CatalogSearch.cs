using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The rules of a hardware catalogue search: what counts as a filter, and how much comes back.
    /// </summary>
    /// <remarks>
    /// Measured 2026-10-20 on TIA Portal V20: <c>HardwareCatalog.Find</c> matches order numbers,
    /// product names and descriptions, and one CPU model alone returns 32 entries, one per firmware.
    /// A short filter can therefore return a large part of the catalogue, and all of it would land in
    /// the model's context. The cap keeps the answer readable and the result says when it cut, so a
    /// caller narrows the filter instead of believing the list is complete. Kept apart from Openness
    /// so the rules are tested without TIA Portal.
    /// </remarks>
    public static class CatalogSearch
    {
        /// <summary>The most entries one search returns.</summary>
        public const int MaximumEntries = 100;

        /// <summary>Checks a filter and returns it trimmed.</summary>
        /// <param name="filter">An order number, a product name or part of either.</param>
        /// <returns>The filter without surrounding white space.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is blank.</exception>
        public static string NormalizeFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "filter is required: an order number such as '6AV2 124-0JC01' or a product name such as 'TP900 Comfort'");
            }

            return filter.Trim();
        }

        /// <summary>Describes the first <see cref="MaximumEntries"/> entries and only counts the rest.</summary>
        /// <typeparam name="TEntry">What the catalogue returned.</typeparam>
        /// <param name="found">Everything the catalogue matched, in its order.</param>
        /// <param name="describe">Turns one entry into its description.</param>
        /// <returns>The entries to show and the total found.</returns>
        /// <remarks>
        /// Describing an Openness catalogue entry is five calls into TIA Portal, made while every other
        /// tool waits on the Openness gate, so entries past the cap are counted and never read.
        /// </remarks>
        public static CatalogSearchResult Limit<TEntry>(ICollection<TEntry> found, Func<TEntry, CatalogEntryInfo> describe)
        {
            if (found == null)
            {
                throw new ArgumentNullException(nameof(found));
            }

            if (describe == null)
            {
                throw new ArgumentNullException(nameof(describe));
            }

            return new CatalogSearchResult(found.Take(MaximumEntries).Select(describe).ToList(), found.Count);
        }
    }
}
