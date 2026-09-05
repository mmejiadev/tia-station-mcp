using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The other class this session moved out of the licensed assembly. It decides what a snapshot
    /// report says about a run, and every one of its decisions is string handling that needed no
    /// TIA Portal to check — but could not be reached without one.
    /// </remarks>
    [TestClass]
    public sealed class SnapshotReportBuilderTests
    {
        private const string Root = @"C:\snapshots\run-1";

        /// <remarks>
        /// Forward slashes and no root, so a report produced on one machine can be compared with one
        /// produced on another. A snapshot report full of absolute paths diffs against nothing.
        /// </remarks>
        [TestMethod]
        public void AddExported_AFileUnderTheRoot_IsRecordedRelativeAndWithForwardSlashes()
        {
            var builder = new SnapshotReportBuilder(Root);

            builder.AddExported(new FileInfo(Path.Combine(Root, "blocks", "1_Tests", "FC_Block_1.scl")));

            Assert.AreEqual("blocks/1_Tests/FC_Block_1.scl", builder.Build().Exported.Single());
        }

        [TestMethod]
        public void AddExported_ARootWithATrailingSeparator_DoesNotLeaveALeadingSlash()
        {
            var builder = new SnapshotReportBuilder(Root + Path.DirectorySeparatorChar);

            builder.AddExported(new FileInfo(Path.Combine(Root, "blocks", "FC.scl")));

            Assert.AreEqual("blocks/FC.scl", builder.Build().Exported.Single());
        }

        /// <remarks>
        /// Windows paths differ in case without differing, and the exporter builds its paths from
        /// what Openness returns rather than from the root string it was given.
        /// </remarks>
        [TestMethod]
        public void AddExported_AFileWhoseRootDiffersOnlyInCase_IsStillRecordedRelative()
        {
            var builder = new SnapshotReportBuilder(Root);

            builder.AddExported(new FileInfo(Path.Combine(@"C:\SNAPSHOTS\RUN-1", "blocks", "FC.scl")));

            Assert.AreEqual("blocks/FC.scl", builder.Build().Exported.Single());
        }

        /// <remarks>
        /// A file that is not under the root is reported whole rather than mangled into something
        /// that looks relative and is not. It should not happen; if it does, the report says so.
        /// </remarks>
        [TestMethod]
        public void AddExported_AFileOutsideTheRoot_IsRecordedWhole()
        {
            var builder = new SnapshotReportBuilder(Root);

            builder.AddExported(new FileInfo(@"D:\elsewhere\FC.scl"));

            StringAssert.Contains(builder.Build().Exported.Single(), "elsewhere", StringComparison.Ordinal);
        }

        /// <remarks>
        /// An inconsistent item is not a failure: TIA Portal refuses to export one, the caller is
        /// told to compile and export again, and the two lists mean different things.
        /// </remarks>
        [TestMethod]
        public void Build_EachKindOfOutcome_LandsInItsOwnList()
        {
            var builder = new SnapshotReportBuilder(Root);

            builder.AddExported(new FileInfo(Path.Combine(Root, "FC.scl")));
            builder.AddInconsistent("FB_Station");
            builder.AddUnsupported("FC_Ladder", "LAD");
            builder.AddFailure("DB_Cell", "the file was locked");

            var result = builder.Build();

            Assert.AreEqual("FC.scl", result.Exported.Single());
            Assert.AreEqual("FB_Station", result.Inconsistent.Single());
            Assert.AreEqual("FC_Ladder (LAD)", result.Unsupported.Single());
            Assert.AreEqual("DB_Cell: the file was locked", result.Failed.Single());
        }

        /// <remarks>
        /// A block with no text representation makes the snapshot incomplete, so the report names
        /// the language too: "it is LAD" is the answer, and it is not a fixable failure.
        /// </remarks>
        [TestMethod]
        public void AddUnsupported_ABlock_NamesTheLanguageThatCannotBeExported()
        {
            var builder = new SnapshotReportBuilder(Root);

            builder.AddUnsupported("FC_Ladder", "GRAPH");

            StringAssert.Contains(builder.Build().Unsupported.Single(), "GRAPH", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Build_NothingAdded_IsEmptyRatherThanNull()
        {
            var result = new SnapshotReportBuilder(Root).Build();

            Assert.AreEqual(0, result.Exported.Count);
            Assert.AreEqual(0, result.Inconsistent.Count);
            Assert.AreEqual(0, result.Unsupported.Count);
            Assert.AreEqual(0, result.Failed.Count);
        }
    }
}
