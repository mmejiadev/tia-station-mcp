using System.IO;
using System.IO.Compression;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Reading a global library and copying its types into a project (ROADMAP H6).</summary>
    /// <remarks>Measured on 2026-10-30: the types a faceplate depends on come with it, and a global library opened read-only is enough.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test55LibraryTypes
    {
        private const string Faceplate = "FACEPLATE_MOTOR_V1";

        private static readonly string[] OnlyFaceplate = { Faceplate };

        private static readonly string[] NoSuchType = { "No_Such_Type" };

        private static readonly string[] OnlyDataType = { "UDT_MOTOR_V1" };

        private string _testDirectory = string.Empty;

        private string _libraryPath = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            var libraryDirectory = Path.Combine(_testDirectory, "library");
            ZipFile.ExtractToDirectory(Settings.FaceplateLibraryZipPath, libraryDirectory);
            _libraryPath = Directory.GetFiles(libraryDirectory, $"*.al{Engineering.TiaMajorVersion}", SearchOption.AllDirectories).Single();

            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "LibraryTypes");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetLibraryTypes_AGlobalLibrary_GivesEachReleasedFaceplateItsReference()
        {
            var types = AssemblyHooks.SharedPortal.GetLibraryTypes(_libraryPath)["types"]!.AsArray();

            var faceplate = types.Single(type => type!["name"]!.GetValue<string>() == Faceplate)!;
            Assert.AreEqual("V0.0.1\\" + Faceplate, faceplate["versions"]![0]!["faceplateReference"]!.GetValue<string>());
        }

        [TestMethod]
        public void CopyLibraryTypes_AFaceplate_BringsTheDataTypeItDependsOn()
        {
            var copied = AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, OnlyFaceplate, Backup())["copied"]!.AsArray();

            var names = copied.Select(type => type!["name"]!.GetValue<string>()).ToArray();
            CollectionAssert.AreEquivalent(new[] { Faceplate, "UDT_MOTOR_V1" }, names);
        }

        [TestMethod]
        public void CopyLibraryTypes_ATypeTheProjectAlreadyHolds_IsRefusedAndChangesNothing()
        {
            AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, OnlyFaceplate, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, OnlyFaceplate, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(2, AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray().Count);
        }

        [TestMethod]
        public void CopyLibraryTypes_ANameTheGlobalLibraryLacks_IsRefusedNamingWhatItHolds()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, NoSuchType, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, Faceplate, System.StringComparison.Ordinal);
        }

        /// <remarks>
        /// The data type comes only as the faceplate's dependency, and the review of 2026-10-30 found it went
        /// unchecked: copied over the one already there as silently as a type asked for.
        /// </remarks>
        [TestMethod]
        public void CopyLibraryTypes_ADependencyTheProjectAlreadyHolds_IsRefusedNamingIt()
        {
            AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, OnlyDataType, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, OnlyFaceplate, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "'UDT_MOTOR_V1'", System.StringComparison.Ordinal);
            Assert.AreEqual(1, AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray().Count);
        }

        [TestMethod]
        public void CopyLibraryTypes_Copied_KeepsTheProjectLibraryBeforeInTheBackup()
        {
            AssemblyHooks.SharedPortal.CopyLibraryTypes(_libraryPath, OnlyFaceplate, Backup());

            Assert.IsTrue(File.Exists(Path.Combine(Backup(), "project-library.json")));
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup");
        }

        /// <remarks>The library is closed again after each call, so a second call finds it as the first did.</remarks>
        [TestMethod]
        public void GetLibraryTypes_ReadTwice_GivesTheSameTypes()
        {
            var first = AssemblyHooks.SharedPortal.GetLibraryTypes(_libraryPath)["types"]!.AsArray().Count;

            var second = AssemblyHooks.SharedPortal.GetLibraryTypes(_libraryPath)["types"]!.AsArray().Count;

            Assert.AreEqual(first, second);
        }
    }
}
