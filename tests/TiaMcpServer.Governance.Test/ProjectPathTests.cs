using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Every one of these ran only inside TIA Portal until now, because the reading of a path was
    /// three copies of string surgery spread through the lookups. That is audit finding F5: the
    /// logic that decides which block gets overwritten, living where nobody could check it.
    /// </remarks>
    [TestClass]
    public sealed class ProjectPathTests
    {
        [TestMethod]
        public void Parse_APathWithGroups_SeparatesTheNameFromWhatHoldsIt()
        {
            var path = ProjectPath.Parse("Common/CarrierRegister/ML_SubstratState");

            Assert.AreEqual("ML_SubstratState", path.Name);
            Assert.AreEqual("Common/CarrierRegister", path.Parent);
            Assert.IsFalse(path.IsTopLevel);
        }

        [TestMethod]
        public void Parse_ABareName_IsTopLevelAndHasNoParent()
        {
            var path = ProjectPath.Parse("PLC_0");

            Assert.AreEqual("PLC_0", path.Name);
            Assert.AreEqual(string.Empty, path.Parent);
            Assert.IsTrue(path.IsTopLevel);
        }

        /// <remarks>
        /// The three readings this class replaced disagreed here: one dropped the empty segment, one
        /// kept it and found nothing, and one never looked. The forgiving answer is the right one —
        /// a doubled slash is a typing artefact, and refusing it sends the caller to look at the
        /// project rather than at the string.
        /// </remarks>
        [TestMethod]
        [DataRow("Common//CarrierRegister/ML_SubstratState")]
        [DataRow("/Common/CarrierRegister/ML_SubstratState")]
        [DataRow("Common/CarrierRegister/ML_SubstratState/")]
        [DataRow("  Common / CarrierRegister / ML_SubstratState  ")]
        public void Parse_ASlashOrSpaceOutOfPlace_ReadsTheSamePath(string written)
        {
            var path = ProjectPath.Parse(written);

            Assert.AreEqual("Common/CarrierRegister/ML_SubstratState", path.ToString());
        }

        /// <remarks>
        /// The one thing refused. It is not a typo, it is a missing argument, and the category
        /// matters: before this, an empty block path reached the group lookup as "the root" and an
        /// empty name filter matched everything, so GetBlock returned whichever block came first.
        /// </remarks>
        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("//")]
        [DataRow(" / / ")]
        public void Parse_APathThatNamesNothing_ThrowsInvalidParams(string written)
        {
            var failure = Assert.ThrowsException<PortalException>(() => ProjectPath.Parse(written));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_APath_KeepsItsSegmentsInOrder()
        {
            var path = ProjectPath.Parse("Group1/Group1.1/PC-System_1.1");

            CollectionAssert.AreEqual(
                new[] { "Group1", "Group1.1", "PC-System_1.1" },
                path.Segments.ToArray());
        }

        /// <remarks>
        /// A group path is a different question: every program has a root block group, and naming
        /// nothing is how a caller addresses it. An empty path to a block names no block.
        /// </remarks>
        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("/")]
        public void GroupSegments_NoGroupNamed_IsTheRootRatherThanARefusal(string written)
        {
            var segments = ProjectPath.GroupSegments(written);

            Assert.AreEqual(0, segments.Count);
        }

        [TestMethod]
        public void GroupSegments_AGroupPath_IsOneNamePerLevel()
        {
            var segments = ProjectPath.GroupSegments("Common/CarrierRegister");

            CollectionAssert.AreEqual(new[] { "Common", "CarrierRegister" }, segments.ToArray());
        }

        [TestMethod]
        public void Join_ANameUnderAGroup_BuildsThePathThatParsesBackToIt()
        {
            var joined = ProjectPath.Join("1_Tests", "FC_Block_1");

            Assert.AreEqual("1_Tests/FC_Block_1", joined);
            Assert.AreEqual("FC_Block_1", ProjectPath.Parse(joined).Name);
        }

        [TestMethod]
        public void Join_ANameAtTheTopLevel_IsJustTheName()
        {
            Assert.AreEqual("PLC_0", ProjectPath.Join(string.Empty, "PLC_0"));
        }

        /// <remarks>
        /// The property that makes this safe to use everywhere: whatever a caller writes, reading it
        /// and writing it back gives a path that reads the same way again.
        /// </remarks>
        [TestMethod]
        [DataRow("A/B/C")]
        [DataRow("A//B/C/")]
        [DataRow(" A / B / C ")]
        public void ToString_ReparsingIt_GivesTheSamePath(string written)
        {
            var once = ProjectPath.Parse(written).ToString();

            Assert.AreEqual(once, ProjectPath.Parse(once).ToString());
        }
    }
}
