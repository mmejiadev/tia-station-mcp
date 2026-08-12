using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test6Retrieve
    {
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
        }

        [TestMethod]
        public void RetrieveProject_ValidArchive_ReturnsOpenedProjectPath()
        {
            var projectPath = AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, _testDirectory);

            Assert.IsTrue(File.Exists(projectPath), $"Retrieved project file does not exist: {projectPath}");
            Assert.IsTrue(Portal.IsLocalProjectFile(projectPath), $"Retrieved path is not a project file: {projectPath}");
        }

        [TestMethod]
        public void RetrieveProject_MissingArchive_ThrowsNotFound()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.RetrieveProject(Path.Combine(_testDirectory, "DoesNotExist.zap20"), _testDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void RetrieveProject_ExistingTarget_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, _testDirectory);

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }
    }
}
