using System.IO;
using System.IO.Compression;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test64GlobalLibraryUpgrade
    {
        private string _testDirectory = string.Empty;
        private string _olderLibrary = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            var libraryDirectory = Path.Combine(_testDirectory, "library");
            ZipFile.ExtractToDirectory(Settings.OlderVersionLibraryZipPath, libraryDirectory);
            _olderLibrary = Directory.GetFiles(libraryDirectory, "*.al20", SearchOption.AllDirectories).Single();
        }

        [TestMethod]
        public void UpgradeGlobalLibrary_OlderLibrary_WritesACurrentCopy()
        {
            var (libraryPath, _) = AssemblyHooks.SharedPortal.UpgradeGlobalLibrary(_olderLibrary);

            Assert.IsTrue(File.Exists(libraryPath), $"Upgraded library file does not exist: {libraryPath}");
            Assert.AreEqual(Engineering.TiaMajorVersion, ProjectUpgradeRule.MajorVersionOf(libraryPath));
        }

        [TestMethod]
        public void UpgradeGlobalLibrary_CopyAlreadyThere_ThrowsInvalidState()
        {
            Directory.CreateDirectory(ProjectUpgradeRule.UpgradedFolder(_olderLibrary, Engineering.TiaMajorVersion));

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.UpgradeGlobalLibrary(_olderLibrary));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }

        [TestMethod]
        public void GetLibraryTypes_OlderLibrary_IsRefusedAndNamesTheUpgrade()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetLibraryTypes(_olderLibrary));

            StringAssert.Contains(exception.Message, "UpgradeGlobalLibrary");
        }
    }
}
