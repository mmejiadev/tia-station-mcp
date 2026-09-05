using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// This rule used to be a private method inside the exporter, in the assembly that needs TIA
    /// Portal to build. It decides what a snapshot file is called, which is what every diff between
    /// two revisions of a project is read from.
    /// </remarks>
    [TestClass]
    public sealed class SnapshotFileNameTests
    {
        [TestMethod]
        public void For_AnOrdinaryName_IsLeftAlone()
        {
            Assert.AreEqual("FC_Block_1", SnapshotFileName.For("FC_Block_1"));
        }

        /// <remarks>
        /// TIA names are freer than file names: quoted, a block can carry a colon or a slash, and
        /// neither can be a file on Windows.
        /// </remarks>
        [TestMethod]
        public void For_ANameAFileSystemRefuses_ReplacesOnlyWhatItRefuses()
        {
            var sanitised = SnapshotFileName.For("FC: v2");

            Assert.AreEqual("FC_ v2", sanitised);
            Assert.IsFalse(
                sanitised.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0,
                "a character the file system refuses survived");
        }

        [TestMethod]
        [DataRow("Motor/Feeder")]
        [DataRow(@"Motor\Feeder")]
        [DataRow("FC?")]
        [DataRow("FC|1")]
        public void For_AnyRefusedCharacter_LeavesANameAFileCanCarry(string name)
        {
            var sanitised = SnapshotFileName.For(name);

            Assert.IsFalse(sanitised.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0, sanitised);
        }

        /// <remarks>
        /// Stated as a test rather than left as a surprise: the mapping is lossy, and two names can
        /// arrive at one file. Nothing here makes them unique — a snapshot is diffed against the
        /// last one, so renaming a file silently would produce a diff nobody can read. The collision
        /// is caught where the writing happens, by SnapshotReportBuilder.TryClaim, and reported.
        /// </remarks>
        [TestMethod]
        public void For_TwoNamesDifferingOnlyInRefusedCharacters_ArriveAtTheSameFileName()
        {
            Assert.AreEqual(SnapshotFileName.For("FC: v2"), SnapshotFileName.For("FC* v2"));
        }

        [TestMethod]
        public void For_AnEmptyName_IsLeftAsItIs()
        {
            Assert.AreEqual(string.Empty, SnapshotFileName.For(string.Empty));
        }
    }
}
