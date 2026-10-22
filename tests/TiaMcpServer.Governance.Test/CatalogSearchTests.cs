using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A catalogue search with no filter would ask for the whole catalogue, and an uncapped one floods
    /// the caller's context. These tests are the two rules' assertion.
    /// </remarks>
    [TestClass]
    public sealed class CatalogSearchTests
    {
        [TestMethod]
        public void NormalizeFilter_BlankFilter_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => CatalogSearch.NormalizeFilter("   "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void NormalizeFilter_PaddedFilter_ReturnsItTrimmed()
        {
            var filter = CatalogSearch.NormalizeFilter("  6AV2 124-0JC01 ");

            Assert.AreEqual("6AV2 124-0JC01", filter);
        }

        [TestMethod]
        public void Limit_MoreThanTheCap_KeepsTheFirstAndCountsAll()
        {
            var found = Entries(CatalogSearch.MaximumEntries + 5);

            var result = CatalogSearch.Limit(found, entry => entry);

            Assert.AreEqual(CatalogSearch.MaximumEntries, result.Entries.Count);
            Assert.AreEqual(CatalogSearch.MaximumEntries + 5, result.TotalFound);
            Assert.IsTrue(result.IsTruncated);
            Assert.AreSame(found[0], result.Entries[0]);
        }

        [TestMethod]
        public void Limit_FewerThanTheCap_KeepsAllAndIsNotTruncated()
        {
            var result = CatalogSearch.Limit(Entries(3), entry => entry);

            Assert.AreEqual(3, result.Entries.Count);
            Assert.IsFalse(result.IsTruncated);
        }

        /// <remarks>
        /// Each description is five calls into TIA Portal under the Openness gate, so entries past the
        /// cap must be counted without being read.
        /// </remarks>
        [TestMethod]
        public void Limit_MoreThanTheCap_DescribesOnlyTheEntriesItKeeps()
        {
            var described = 0;

            CatalogSearch.Limit(Entries(CatalogSearch.MaximumEntries * 3), entry => { described++; return entry; });

            Assert.AreEqual(CatalogSearch.MaximumEntries, described);
        }

        private static List<CatalogEntryInfo> Entries(int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => new CatalogEntryInfo("6ES7 000-0AA00-0AA0", $"V{index}", $"OrderNumber:6ES7 000-0AA00-0AA0/V{index}", "Root", "test"))
                .ToList();
        }
    }
}
