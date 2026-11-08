using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-11-01 on TIA Portal V20: <c>CreateFrom</c> in its default mode renames a master copy's object without a word.</remarks>
    [TestClass]
    public sealed class MasterCopyRulesTests
    {
        [TestMethod]
        public void ParseKind_EachNameCaseAside_IsRead()
        {
            var kinds = new[] { MasterCopyRules.ParseKind("block"), MasterCopyRules.ParseKind("TAGTABLE"), MasterCopyRules.ParseKind(" plcType ") };

            CollectionAssert.AreEqual(new[] { MasterCopySourceKind.Block, MasterCopySourceKind.TagTable, MasterCopySourceKind.PlcType }, kinds);
        }

        [TestMethod]
        public void ParseKind_AnUnknownKind_IsRefusedNamingTheAcceptedOnes()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.ParseKind("screen"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "block, tagTable, plcType", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Name_EveryKind_IsReadBackByParseKind()
        {
            foreach (MasterCopySourceKind kind in Enum.GetValues(typeof(MasterCopySourceKind)))
            {
                Assert.AreEqual(kind, MasterCopyRules.ParseKind(MasterCopyRules.Name(kind)));
            }
        }

        /// <remarks>The rule against a silent default: a kind added later must refuse, never pass as one of these.</remarks>
        [TestMethod]
        public void Name_AnUnrecognisedKind_IsRefusedAsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.Name((MasterCopySourceKind)99));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void FolderSegments_ANestedPath_IsSplitOutermostFirst()
        {
            var segments = MasterCopyRules.FolderSegments(" /Motors/ Pumps /");

            CollectionAssert.AreEqual(new[] { "Motors", "Pumps" }, new System.Collections.Generic.List<string>(segments));
        }

        [TestMethod]
        public void FolderSegments_Empty_IsTheTopFolder()
        {
            Assert.AreEqual(0, MasterCopyRules.FolderSegments(string.Empty).Count);
        }

        [TestMethod]
        public void FolderSegments_AParentStep_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.FolderSegments("Motors/../Valves"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void FolderSegments_AnEmptyNameBetweenSeparators_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.FolderSegments("Motors//Pumps"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void ParseMasterCopyPath_AFolderAndAName_AreSplit()
        {
            var (folder, name) = MasterCopyRules.ParseMasterCopyPath("Motors/FB_Motor");

            CollectionAssert.AreEqual(new[] { "Motors" }, new System.Collections.Generic.List<string>(folder));
            Assert.AreEqual("FB_Motor", name);
        }

        [TestMethod]
        public void ParseMasterCopyPath_Empty_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.ParseMasterCopyPath(" "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void CheckNotPresent_ANameAlreadyThereCaseAside_IsRefusedAsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.CheckNotPresent("fb_motor", new[] { "Main", "FB_Motor" }, "The program 'PLC_1'"));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "rename the new one", StringComparison.Ordinal);
        }

        [TestMethod]
        public void FolderPlace_NoNames_IsTheTopFolder()
        {
            StringAssert.Contains(MasterCopyRules.FolderPlace(Array.Empty<string>()), "top master copy folder", StringComparison.Ordinal);
        }

        [TestMethod]
        public void List_NoNames_SaysNothing()
        {
            Assert.AreEqual("nothing", MasterCopyRules.List(Array.Empty<string>()));
        }

        [TestMethod]
        public void CheckNotPresent_ANewName_Passes()
        {
            MasterCopyRules.CheckNotPresent("FB_Valve", new[] { "Main", "FB_Motor" }, "The program 'PLC_1'");
        }

        /// <remarks>Measured 2026-11-08 on TIA Portal V20: MC_Running, already in the program, was created as MC_Running(1).</remarks>
        [TestMethod]
        public void RenamedOnCreate_ANameTheProgramHeldWithANumberInBrackets_IsFound()
        {
            var renamed = MasterCopyRules.RenamedOnCreate(new[] { "MC_Running(1)", "MC_Stop" }, new[] { "mc_running", "Start" });

            Assert.AreEqual(1, renamed.Count);
            Assert.AreEqual(("MC_Running(1)", "MC_Running"), renamed[0]);
        }

        [TestMethod]
        public void RenamedOnCreate_ABracketedNameTheProgramDidNotHold_IsNotARename()
        {
            var renamed = MasterCopyRules.RenamedOnCreate(new[] { "Valve(2)" }, new[] { "Start" });

            Assert.AreEqual(0, renamed.Count);
        }

        [TestMethod]
        public void RequireSingleKind_OneBlock_IsABlock()
        {
            var kind = MasterCopyRules.RequireSingleKind("FB_Motor", new (string, MasterCopySourceKind?)[] { ("FB", MasterCopySourceKind.Block) });

            Assert.AreEqual(MasterCopySourceKind.Block, kind);
        }

        /// <remarks>Creating part of a master copy would leave the rest behind without a word.</remarks>
        [TestMethod]
        public void RequireSingleKind_TwoItems_IsRefused()
        {
            var contents = new (string, MasterCopySourceKind?)[] { ("FB", MasterCopySourceKind.Block), ("GlobalDB", MasterCopySourceKind.Block) };

            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.RequireSingleKind("Motor kit", contents));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void RequireSingleKind_NoItem_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.RequireSingleKind("Empty", Array.Empty<(string, MasterCopySourceKind?)>()));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void RequireSingleKind_AnItemOfAnotherKind_IsRefusedNamingItsType()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => MasterCopyRules.RequireSingleKind("Watch", new (string, MasterCopySourceKind?)[] { ("PlcWatchTable", null) }));

            StringAssert.Contains(refusal.Message, "PlcWatchTable", StringComparison.Ordinal);
        }
    }
}
