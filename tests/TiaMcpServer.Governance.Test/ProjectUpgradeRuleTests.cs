using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The safety rule here: an upgrade writes a new project to disk, so it never happens as a side
    /// effect of opening. Before it, OpenProject upgraded an older project without a word.
    /// </remarks>
    [TestClass]
    public sealed class ProjectUpgradeRuleTests
    {
        private const int V21 = 21;
        private const string OlderProject = @"C:\Class\Motor\Motor.ap20";
        private const string CurrentProject = @"C:\Class\Motor_V21\Motor_V21.ap21";
        private const string OlderArchive = @"C:\Class\Motor.zap20";

        [TestMethod]
        [DataRow(OlderProject, 20)]
        [DataRow(CurrentProject, 21)]
        [DataRow(OlderArchive, 20)]
        [DataRow(@"C:\Class\MOTOR.ZAP19", 19)]
        public void MajorVersionOf_ProjectOrArchive_ReadsTheExtension(string path, int expected)
        {
            var version = ProjectUpgradeRule.MajorVersionOf(path);

            Assert.AreEqual(expected, version);
        }

        [TestMethod]
        [DataRow(@"C:\Class\Motor.als21")]
        [DataRow(@"C:\Class\Motor.txt")]
        [DataRow(@"C:\Class\ap21")]
        [DataRow("")]
        public void MajorVersionOf_AnythingElse_IsNull(string path)
        {
            var version = ProjectUpgradeRule.MajorVersionOf(path);

            Assert.IsNull(version);
        }

        [TestMethod]
        public void RequireNoUpgradeNeeded_OlderProject_RefusesAndNamesTheTool()
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireNoUpgradeNeeded(OlderProject, V21, "OpenProjectWithUpgrade"));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "OpenProjectWithUpgrade", StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow(CurrentProject)]
        [DataRow(@"C:\Class\Motor.als20")]
        public void RequireNoUpgradeNeeded_CurrentProjectOrSession_Passes(string path)
        {
            ProjectUpgradeRule.RequireNoUpgradeNeeded(path, V21, "OpenProjectWithUpgrade");
        }

        [TestMethod]
        public void RequireUpgradableArchive_OlderArchive_Passes()
        {
            ProjectUpgradeRule.RequireUpgradableArchive(OlderArchive, V21);
        }

        [TestMethod]
        public void RequireUpgradableProject_OlderProject_Passes()
        {
            ProjectUpgradeRule.RequireUpgradableProject(OlderProject, V21);
        }

        [TestMethod]
        [DataRow(CurrentProject)]
        [DataRow(@"C:\Class\Motor\Motor.ap22")]
        public void RequireUpgradableProject_CurrentOrNewerFile_IsRefused(string path)
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireUpgradableProject(path, V21));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        [DataRow(@"C:\Class\Motor.txt")]
        [DataRow(OlderArchive)]
        public void RequireUpgradableProject_NotAProjectFile_IsInvalidParams(string path)
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireUpgradableProject(path, V21));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        [DataRow(@"C:\Class\Motor.txt")]
        [DataRow(OlderProject)]
        public void RequireUpgradableArchive_NotAnArchive_IsInvalidParams(string path)
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireUpgradableArchive(path, V21));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void RequireUpgradableArchive_CurrentArchive_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireUpgradableArchive(@"C:\Class\Motor.zap21", V21));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void RequireUpgradableLibrary_OlderLibrary_Passes()
        {
            ProjectUpgradeRule.RequireUpgradableLibrary(@"C:\Libraries\Kit\Kit.al20", V21);
        }

        [TestMethod]
        [DataRow(OlderProject)]
        [DataRow(OlderArchive)]
        public void RequireUpgradableLibrary_NotALibrary_IsInvalidParams(string path)
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireUpgradableLibrary(path, V21));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void RequireNoUpgradeNeeded_OlderLibrary_RefusesAndNamesTheTool()
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => ProjectUpgradeRule.RequireNoUpgradeNeeded(@"C:\Libraries\Kit\Kit.al20", V21, "UpgradeGlobalLibrary"));

            StringAssert.Contains(refusal.Message, "UpgradeGlobalLibrary", StringComparison.Ordinal);
        }

        [TestMethod]
        public void UpgradedFolder_ProjectInAFolder_IsNamedAfterTheFolder()
        {
            var folder = ProjectUpgradeRule.UpgradedFolder(@"C:\Class\copy\Motor.ap20", V21);

            Assert.AreEqual(@"C:\Class\copy_V21", folder);
        }
    }
}
