using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test63ArchiveProject
    {
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestMethod]
        public void ArchiveProject_OpenProject_WritesAnArchiveRetrieveOpens()
        {
            var archivePath = AssemblyHooks.SharedPortal.ArchiveProject(_testDirectory, "Archived", discardRestorableData: true);

            var retrieved = AssemblyHooks.SharedPortal.RetrieveProject(archivePath, Path.Combine(_testDirectory, "retrieved"));

            Assert.IsTrue(File.Exists(retrieved), $"The archive did not retrieve to a project: {retrieved}");
        }

        [TestMethod]
        public void ArchiveProject_ArchiveAlreadyThere_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.ArchiveProject(_testDirectory, "Twice", discardRestorableData: true);

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ArchiveProject(_testDirectory, "Twice", discardRestorableData: true));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }

        [TestMethod]
        public void ArchiveProject_NameWithExtension_ThrowsInvalidParams()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ArchiveProject(_testDirectory, "Archived.zap21", discardRestorableData: true));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }
    }
}
