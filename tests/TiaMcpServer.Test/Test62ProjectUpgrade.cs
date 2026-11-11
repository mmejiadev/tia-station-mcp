using System.IO;
using System.Security.Cryptography;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test62ProjectUpgrade
    {
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
        }

        [TestMethod]
        public void RetrieveProjectWithUpgrade_OlderArchive_OpensACurrentProject()
        {
            var projectPath = AssemblyHooks.SharedPortal.RetrieveProjectWithUpgrade(Settings.OlderVersionArchivePath, _testDirectory);

            Assert.IsTrue(File.Exists(projectPath), $"Upgraded project file does not exist: {projectPath}");
            Assert.AreEqual(Engineering.TiaMajorVersion, ProjectUpgradeRule.MajorVersionOf(projectPath));
        }

        [TestMethod]
        public void RetrieveProjectWithUpgrade_OlderArchive_LeavesTheArchiveUnchanged()
        {
            var before = HashOf(Settings.OlderVersionArchivePath);

            AssemblyHooks.SharedPortal.RetrieveProjectWithUpgrade(Settings.OlderVersionArchivePath, _testDirectory);

            CollectionAssert.AreEqual(before, HashOf(Settings.OlderVersionArchivePath));
        }

        [TestMethod]
        public void RetrieveProjectWithUpgrade_TargetNotEmpty_ThrowsInvalidState()
        {
            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(Path.Combine(_testDirectory, "already-here.txt"), "kept");

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.RetrieveProjectWithUpgrade(Settings.OlderVersionArchivePath, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }

        [TestMethod]
        public void RetrieveProject_OlderArchive_IsRefusedAndNamesTheUpgrade()
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.RetrieveProject(Settings.OlderVersionArchivePath, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "RetrieveProjectWithUpgrade");
        }

        [TestMethod]
        public void OpenProject_OlderProject_IsRefusedAndNamesTheUpgrade()
        {
            var refusal = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.OpenProject(Path.Combine(_testDirectory, "Older", "Older.ap20")));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "OpenProjectWithUpgrade");
        }

        [TestMethod]
        public void OpenProjectWithUpgrade_OlderProject_OpensACurrentCopy()
        {
            var olderProject = ExtractOlderProject();

            var projectPath = AssemblyHooks.SharedPortal.OpenProjectWithUpgrade(olderProject);

            Assert.IsTrue(File.Exists(projectPath), $"Upgraded project file does not exist: {projectPath}");
            Assert.AreEqual(Engineering.TiaMajorVersion, ProjectUpgradeRule.MajorVersionOf(projectPath));
        }

        [TestMethod]
        public void OpenProjectWithUpgrade_CopyAlreadyExists_ThrowsInvalidState()
        {
            var olderProject = ExtractOlderProject();
            Directory.CreateDirectory(ProjectUpgradeRule.UpgradedFolder(olderProject, Engineering.TiaMajorVersion));

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.OpenProjectWithUpgrade(olderProject));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }

        /// <remarks>A retrieve with upgrade leaves the project as extracted beside the copy: a real older project, with no second fixture.</remarks>
        private string ExtractOlderProject()
        {
            AssemblyHooks.SharedPortal.RetrieveProjectWithUpgrade(Settings.OlderVersionArchivePath, _testDirectory);
            AssemblyHooks.SharedPortal.CloseProject();

            var extracted = Path.Combine(_testDirectory, "TestProject1");
            var moved = Path.Combine(_testDirectory, "older", "TestProject1");

            Directory.CreateDirectory(Path.Combine(_testDirectory, "older"));
            Directory.Move(extracted, moved);

            return Path.Combine(moved, "TestProject1.ap20");
        }

        private static byte[] HashOf(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();

            return sha.ComputeHash(stream);
        }
    }
}
