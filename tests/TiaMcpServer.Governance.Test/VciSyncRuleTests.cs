using System;
using TiaMcpServer.Siemens;
using TiaMcpServer.Vci;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A synchronisation copies one side over the other whatever changed, so the rule that nothing is copied when any
    /// object would lose a change is the one thing between a sync and a lost edit. Each test names a case of it.
    /// </remarks>
    [TestClass]
    public sealed class VciSyncRuleTests
    {
        private static readonly VciObjectState MainChangedInProject = new VciObjectState("PLC_1/Blocks/Main.xml", "Unequal", "ProjectObjectChanged");
        private static readonly VciObjectState MotorChangedInFile = new VciObjectState("PLC_1/Blocks/Motor.xml", "Unequal", "WorkspaceFileChanged");
        private static readonly VciObjectState TagsEqual = new VciObjectState("PLC_1/PLC tags/Default tag table.xml", "Equal", "None");

        [TestMethod]
        public void Plan_ProjectToWorkspace_TakesWhatChangedInTheProjectAndLeavesTheEqual()
        {
            var files = VciSyncRule.Plan(new[] { MainChangedInProject, TagsEqual }, VciDirection.ProjectToWorkspace);

            CollectionAssert.AreEqual(new[] { "PLC_1/Blocks/Main.xml" }, (System.Collections.ICollection)files);
        }

        [TestMethod]
        public void Plan_WorkspaceToProject_TakesWhatChangedInTheWorkspace()
        {
            var files = VciSyncRule.Plan(new[] { MotorChangedInFile, TagsEqual }, VciDirection.WorkspaceToProject);

            CollectionAssert.AreEqual(new[] { "PLC_1/Blocks/Motor.xml" }, (System.Collections.ICollection)files);
        }

        /// <remarks>Copying the project out would overwrite the file somebody edited: nothing at all is copied.</remarks>
        [TestMethod]
        public void Plan_ProjectToWorkspaceWithAFileEditedInTheWorkspace_ThrowsInvalidStateNamingIt()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => VciSyncRule.Plan(new[] { MainChangedInProject, MotorChangedInFile }, VciDirection.ProjectToWorkspace));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "Motor.xml", StringComparison.Ordinal);
            StringAssert.Contains(refusal.Message, "Nothing was synchronised", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Plan_WorkspaceToProjectWithABlockEditedInTheProject_ThrowsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => VciSyncRule.Plan(new[] { MainChangedInProject }, VciDirection.WorkspaceToProject));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        [DataRow("Unknown", "None")]
        [DataRow("WorkspaceFileMissing", "None")]
        [DataRow("Unequal", "None")]
        [DataRow("Unequal", "SomethingNew")]
        public void Plan_AStateNotDecidedHere_ThrowsInvalidState(string compareState, string changedSide)
        {
            var odd = new VciObjectState("PLC_1/Blocks/Odd.xml", compareState, changedSide);

            Assert.ThrowsException<PortalException>(() => VciSyncRule.Plan(new[] { odd }, VciDirection.ProjectToWorkspace));
        }

        [TestMethod]
        public void Plan_EverythingEqual_CopiesNothing()
        {
            var files = VciSyncRule.Plan(new[] { TagsEqual }, VciDirection.WorkspaceToProject);

            Assert.AreEqual(0, files.Count);
        }

        [TestMethod]
        public void Plan_AnUndefinedDirection_ThrowsInvalidState()
        {
            Assert.ThrowsException<PortalException>(() => VciSyncRule.Plan(new[] { TagsEqual }, (VciDirection)42));
        }
    }
}
