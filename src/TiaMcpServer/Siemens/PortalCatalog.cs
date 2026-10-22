using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW.HardwareCatalog;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The hardware catalogue: what this TIA Portal installation can add. Reading it needs a
    /// connection and no project, because the catalogue belongs to the installation.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Searches the hardware catalogue.</summary>
        /// <param name="filter">An order number, a product name, or part of either.</param>
        /// <returns>The matching entries, capped at <see cref="CatalogSearch.MaximumEntries"/>, and how many there were.</returns>
        /// <exception cref="PortalException">Not connected, or the filter is blank.</exception>
        /// <remarks>
        /// The first search of a session loads the catalogue and took 31 s on 2026-10-20; the ones
        /// after it took a tenth of a second.
        /// </remarks>
        public CatalogSearchResult FindCatalogEntries(string filter)
        {
            _logger?.LogInformation("Searching the hardware catalogue for {Filter}...", filter);

            try
            {
                var normalized = CatalogSearch.NormalizeFilter(filter);

                if (IsPortalNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Connect to TIA Portal before searching the hardware catalogue");
                }

                return CatalogSearch.Limit(_portal!.HardwareCatalog.Find(normalized), ToCatalogEntryInfo);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Searching the hardware catalogue failed: {ex.Message}", null, ex);

                pex.Data["filter"] = filter;

                _logger?.LogError(pex, "FindCatalogEntries failed for {Filter}", filter);
                throw pex;
            }
        }

        private static CatalogEntryInfo ToCatalogEntryInfo(CatalogEntry entry)
        {
            return new CatalogEntryInfo(
                entry.ArticleNumber ?? string.Empty,
                entry.Version ?? string.Empty,
                entry.TypeIdentifier ?? string.Empty,
                entry.CatalogPath ?? string.Empty,
                entry.Description ?? string.Empty);
        }
    }
}
