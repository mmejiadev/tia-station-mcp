using System;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The library targets a policy file names: a type is under its own prefix, apart from any PLC or HMI.</remarks>
    [TestClass]
    public sealed class ChangeTargetLibraryTests
    {
        [TestMethod]
        public void LibraryType_AType_IsUnderTheLibraryPrefix()
        {
            Assert.AreEqual("library/FP_Motor", ChangeTarget.LibraryType("FP_Motor"));
        }

        [TestMethod]
        public void LibraryType_APolicyForAPlcNamedLikeTheType_DoesNotMatchIt()
        {
            var plcRule = TargetPattern.Parse("FP_Motor/*");

            Assert.IsFalse(plcRule.Matches(ChangeTarget.LibraryType("FP_Motor")));
        }

        [TestMethod]
        public void ProjectLibrary_ARuleForEveryType_DoesNotAllowCopyingIntoTheLibrary()
        {
            var typeRule = TargetPattern.Parse("library/*");

            Assert.IsFalse(typeRule.Matches(ChangeTarget.ProjectLibrary));
        }

        /// <remarks>Adding types to the library must not grant deleting them in bulk (review of 2026-11-15).</remarks>
        [TestMethod]
        public void ProjectLibraryCleanUp_ARuleAllowingTheLibrary_DoesNotAllowCleaningItUp()
        {
            var libraryRule = TargetPattern.Parse(ChangeTarget.ProjectLibrary);
            var typeRule = TargetPattern.Parse("library/*");

            Assert.IsFalse(libraryRule.Matches(ChangeTarget.ProjectLibraryCleanUp));
            Assert.IsFalse(typeRule.Matches(ChangeTarget.ProjectLibraryCleanUp));
        }

        /// <remarks>Editing and releasing a type must not grant deleting it (review of 2026-11-15).</remarks>
        [TestMethod]
        public void LibraryTypeDeletion_ARuleForEditingTypes_DoesNotAllowDeletingThem()
        {
            var editRule = TargetPattern.Parse("library/*");

            Assert.IsFalse(editRule.Matches(ChangeTarget.LibraryTypeDeletion("FP_Motor")));
        }

        /// <remarks>Saving the project must not grant putting script into its HMIs (review of 2026-11-15).</remarks>
        [TestMethod]
        public void CustomWebControl_ARuleAllowingTheProject_DoesNotAllowInstallingControls()
        {
            var projectRule = TargetPattern.Parse(ChangeTarget.Project);

            Assert.IsFalse(projectRule.Matches(ChangeTarget.CustomWebControl("Plant.Pump")));
        }

        /// <remarks>A rule for the catalogue's controls must not reach a definition a session wrote (security review of 2026-11-15).</remarks>
        [TestMethod]
        public void CustomWebControl_ARuleForTheCatalogue_DoesNotReachAnotherControl()
        {
            var catalogueRule = TargetPattern.Parse(ChangeTarget.CustomWebControl("TiaStation.*"));

            Assert.IsTrue(catalogueRule.Matches(ChangeTarget.CustomWebControl("TiaStation.PilotLight")));
            Assert.IsFalse(catalogueRule.Matches(ChangeTarget.CustomWebControl("Plant.Pump")));
        }

        /// <remarks>Pruning old versions must not grant deleting whole types (security review of 2026-11-15).</remarks>
        [TestMethod]
        public void ProjectLibraryCleanUpTypes_ARuleForPruningVersions_DoesNotAllowDeletingTypes()
        {
            var pruneRule = TargetPattern.Parse(ChangeTarget.ProjectLibraryCleanUp);

            Assert.IsFalse(pruneRule.Matches(ChangeTarget.ProjectLibraryCleanUpTypes));
        }

        /// <remarks>The review of 2026-11-01: the target was the file's stem, so a rule for a scratch library reached a shared one of that name.</remarks>
        [TestMethod]
        public void GlobalLibraryFile_ARuleForOneLibrary_DoesNotReachAnotherOfThatName()
        {
            var scratchRule = TargetPattern.Parse(ChangeTarget.GlobalLibraryFolder(@"C:\Scratch\Kit"));

            Assert.IsTrue(scratchRule.Matches(ChangeTarget.GlobalLibraryFile(@"C:\Scratch\Kit\Kit.al20")));
            Assert.IsFalse(scratchRule.Matches(ChangeTarget.GlobalLibraryFile(@"D:\Shared\Kit\Kit.al20")));
            Assert.IsFalse(scratchRule.Matches(ChangeTarget.GlobalLibraryFile(@"\\server\class\Kit\Kit.al20")));
        }

        [TestMethod]
        public void NewGlobalLibrary_OnceCreated_IsTheTargetOfItsFile()
        {
            Assert.AreEqual(ChangeTarget.GlobalLibraryFile(@"C:\Libraries\Kit\Kit.al20"), ChangeTarget.NewGlobalLibrary(@"C:\Libraries\", "Kit"));
        }

        [TestMethod]
        public void NewGlobalLibrary_ANameWithParentSteps_IsWhereItWouldLand()
        {
            Assert.AreEqual("global-library/C:/Shared/Kit", ChangeTarget.NewGlobalLibrary(@"C:\Libraries", @"..\Shared\Kit"));
        }

        [TestMethod]
        public void GlobalLibraryFile_AnUncPath_KeepsItsServer()
        {
            Assert.AreEqual("global-library///server/class/Kit", ChangeTarget.GlobalLibraryFile(@"\\server\class\Kit\Kit.al20"));
        }

        [TestMethod]
        public void GlobalLibraryFile_ParentSteps_CannotWalkOutOfARule()
        {
            var folderRule = TargetPattern.Parse("global-library/C:/Scratch/*");

            Assert.IsFalse(folderRule.Matches(ChangeTarget.GlobalLibraryFile(@"C:\Scratch\..\Shared\Kit\Kit.al20")));
        }

        [TestMethod]
        public void GlobalLibraryFolder_ATrailingSeparatorOrOtherCase_IsTheSameLibrary()
        {
            var rule = TargetPattern.Parse(ChangeTarget.GlobalLibraryFolder(@"C:\Scratch\Kit"));

            Assert.IsTrue(rule.Matches(ChangeTarget.GlobalLibraryFolder(@"c:\scratch\KIT\")));
        }

        [TestMethod]
        public void GlobalLibraryFile_ARelativePath_IsMadeFull()
        {
            var target = ChangeTarget.GlobalLibraryFile(@"Kit\Kit.al20");

            StringAssert.EndsWith(target, "/Kit", StringComparison.Ordinal);
            StringAssert.Contains(target, ":/", StringComparison.Ordinal);
        }

        /// <remarks>Fail closed: no library named is a target no literal rule matches, and the portal then refuses the path itself.</remarks>
        [TestMethod]
        public void GlobalLibraryFile_NoPath_IsNoLibrary()
        {
            Assert.AreEqual("global-library/", ChangeTarget.GlobalLibraryFile(" "));
            Assert.IsFalse(TargetPattern.Parse(ChangeTarget.GlobalLibraryFolder(@"C:\Scratch\Kit")).Matches(ChangeTarget.GlobalLibraryFile(" ")));
        }

        /// <remarks>The review of 2026-11-02: these spell a protected folder so that a deny rule, which compares text, does not see it.</remarks>
        [TestMethod]
        [DataRow(@"\\localhost\C$\Shared\Kit\Kit.al20")]
        [DataRow(@"\\?\C:\Shared\Kit\Kit.al20")]
        [DataRow(@"\\.\C:\Shared\Kit\Kit.al20")]
        [DataRow("//?/C:/Shared/Kit/Kit.al20")]
        public void GlobalLibraryFile_AnotherSpellingOfAPath_IsRefusedAsInvalidParams(string spelled)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => ChangeTarget.GlobalLibraryFile(spelled));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void GlobalLibraryFile_NotAPath_IsRefusedAsInvalidParams()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => ChangeTarget.GlobalLibraryFile("C:\\Kit\u0001\\Kit.al20"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }
    }
}
