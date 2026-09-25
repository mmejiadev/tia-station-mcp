using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The rows of a watch or force table, checked where they can be checked without TIA Portal:
    /// what a row says about itself once it has been read.
    ///
    /// The distinction under test throughout is the one that matters in a workshop. A row that
    /// only watches and a row that has a value prepared are the same object with one flag
    /// different, and the flag is invisible unless something prints it. Nothing here builds a row
    /// to write, because nothing can: Openness creates no row with an address, and rows reach a
    /// table only as an imported document.
    /// </remarks>
    [TestClass]
    public sealed class WatchEntryTests
    {
        /// <remarks>
        /// The line a caller reads. A row that only watches has to say so in words rather than by
        /// leaving a column empty, because an empty column reads as a value nobody could print.
        /// </remarks>
        [TestMethod]
        public void Line_ARowThatOnlyWatches_SaysSoRatherThanLeavingTheColumnEmpty()
        {
            var row = new WatchEntryInfo("%Q0.0", "Bool", "Permanent", WatchEntryIntention.None);

            Assert.AreEqual("%Q0.0 | Bool | Permanent | watch only", row.Line);
        }

        [TestMethod]
        public void Line_ARowWithAValuePrepared_ShowsTheValueAndWhen()
        {
            var row = new WatchEntryInfo("%Q0.0", "Bool", "Permanent", new WatchEntryIntention(true, "TRUE", "OnceOnlyAtStart"));

            Assert.AreEqual("%Q0.0 | Bool | Permanent | TRUE @ OnceOnlyAtStart", row.Line);
        }

        /// <remarks>
        /// A force has no trigger: it holds while it is active. The line must not invent one, and
        /// must not print a dangling separator where a watch row would have had it.
        /// </remarks>
        [TestMethod]
        public void Line_AForcedRow_ShowsTheValueWithNoTrigger()
        {
            var row = new WatchEntryInfo("%Q0.0", "Bool", "Permanent", new WatchEntryIntention(true, "TRUE", string.Empty));

            Assert.AreEqual("%Q0.0 | Bool | Permanent | TRUE", row.Line);
        }

        [TestMethod]
        public void Line_ATable_NamesItsKindAndWhetherItIsConsistent()
        {
            var table = new WatchTableInfo("Cell checks", WatchTableInfo.WatchKind, 3, true);

            Assert.AreEqual("Cell checks | Watch | 3 row(s) | consistent", table.Line);
        }
    }
}
