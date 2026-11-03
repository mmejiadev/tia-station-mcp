using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Test
{
    /// <summary>New versions of a type, instances moved to them, a global library of one's own, a UDT from a library (ROADMAP H6, U8).</summary>
    /// <remarks>On the class project 0967_1. What each refusal stands for was measured on 2026-11-01 by a probe on a copy of it.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test58LibraryVersions
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Motor = "Faceplate_BOTO_MOTOR_V1";

        private const string InWorkOnly = "FACEPLATE_MOTOR_V1";

        private const string DataType = "UDT_MOTOR_V1";

        private static readonly string[] OnlyMotor = { Motor };

        private static readonly string[] OnlyInWorkType = { InWorkOnly };

        private static readonly string[] NoSuchType = { "NoSuchType" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.FaceplateArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void EditLibraryType_AReleasedFaceplate_StartsTheNextVersionInWork()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());

            var type = AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup());

            var last = type["versions"]!.AsArray().Single(version => version!["state"]!.GetValue<string>() == "inWork")!;
            Assert.AreEqual("0.0.2", last["version"]!.GetValue<string>());
        }

        [TestMethod]
        public void EditLibraryType_AVersionAlreadyInWork_IsRefusedAsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "already has a version in work", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void EditLibraryType_APlcDataType_IsRefusedAsNeedingATestInstance()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.EditLibraryType(DataType, Backup()));

            StringAssert.Contains(failure.Message, "test instance", System.StringComparison.Ordinal);
        }

        /// <remarks>A release alone leaves an instance where it is; what its interface was given stays through the move.</remarks>
        [TestMethod]
        public void UpdateUnifiedFaceplates_ANewVersion_MovesTheInstanceAndKeepsItsInterface()
        {
            PlaceOnVersionOne();
            AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup());
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            Assert.AreEqual("0.0.1", Faceplate(AssemblyHooks.SharedPortal.GetUnifiedFaceplates(HmiPath))["version"]!.GetValue<string>());

            var moved = Faceplate(AssemblyHooks.SharedPortal.UpdateUnifiedFaceplates(HmiPath, Motor, null, Backup()));

            Assert.AreEqual("0.0.2", moved["version"]!.GetValue<string>());
            Assert.AreEqual("M2", moved["interface"]!["Interface_MOTOR"]!.GetValue<string>());
        }

        [TestMethod]
        public void UpdateUnifiedFaceplates_ANewVersion_SaysTheVersionMovedTo()
        {
            PlaceOnVersionOne();
            AssemblyHooks.SharedPortal.EditLibraryType(Motor, Backup());
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());

            var moved = AssemblyHooks.SharedPortal.UpdateUnifiedFaceplates(HmiPath, Motor, null, Backup());

            Assert.AreEqual("0.0.2", moved["version"]!.GetValue<string>());
        }

        [TestMethod]
        public void UpdateUnifiedFaceplates_AVersionNotReleased_IsRefusedAndMovesNothing()
        {
            PlaceOnVersionOne();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.UpdateUnifiedFaceplates(HmiPath, Motor, new System.Version(0, 0, 9), Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual("0.0.1", Faceplate(AssemblyHooks.SharedPortal.GetUnifiedFaceplates(HmiPath))["version"]!.GetValue<string>());
        }

        [TestMethod]
        public void UpdateUnifiedFaceplates_NoInstanceOfTheType_IsRefused()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.UpdateUnifiedFaceplates(HmiPath, Motor, null, Backup()));

            StringAssert.Contains(failure.Message, "has no instance", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void PublishLibraryTypes_ANewLibrary_HoldsTheTypeAndWhatItDependsOn()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            var library = NewLibrary();

            var published = AssemblyHooks.SharedPortal.PublishLibraryTypes(library, OnlyMotor, Backup());

            var names = published["types"]!.AsArray().Select(type => type!["name"]!.GetValue<string>()).ToArray();
            CollectionAssert.Contains(names, Motor);
            CollectionAssert.Contains(names, DataType);
            Assert.AreEqual(names.Length, AssemblyHooks.SharedPortal.GetLibraryTypes(library)["types"]!.AsArray().Count, "saved to the file");
        }

        [TestMethod]
        public void PublishLibraryTypes_ATypeOnlyInWork_IsRefusedAndLeavesTheLibraryEmpty()
        {
            var library = NewLibrary();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PublishLibraryTypes(library, OnlyInWorkType, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetLibraryTypes(library)["types"]!.AsArray().Count);
        }

        [TestMethod]
        public void PublishLibraryTypes_Published_ArchivesTheLibraryFirst()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());

            AssemblyHooks.SharedPortal.PublishLibraryTypes(NewLibrary(), OnlyMotor, Path.Combine(_testDirectory, "archive"));

            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(_testDirectory, "archive")).Length);
        }

        /// <remarks>The backup registry's default root is relative, and Openness refuses a relative path: found by the review of 2026-11-01.</remarks>
        [TestMethod]
        public void PublishLibraryTypes_ARelativeBackupDirectory_StillArchivesTheLibrary()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            var library = NewLibrary();
            var workingDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(_testDirectory);

                AssemblyHooks.SharedPortal.PublishLibraryTypes(library, OnlyMotor, "archive");

                Assert.AreEqual(1, Directory.GetFiles(Path.Combine(_testDirectory, "archive")).Length);
            }
            finally
            {
                Directory.SetCurrentDirectory(workingDirectory);
            }
        }

        /// <remarks>TIA Portal refuses an archive path over 143 characters, and a backup folder of the registry is often longer: found by the review of 2026-11-02.</remarks>
        [TestMethod]
        public void PublishLibraryTypes_ABackupDirectoryOver143Characters_StillArchivesTheLibrary()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            // 200 characters in all: over TIA Portal's limit, under the 248 Windows allows a folder.
            var longDirectory = Path.Combine(_testDirectory, new string('a', 199 - _testDirectory.Length));

            AssemblyHooks.SharedPortal.PublishLibraryTypes(NewLibrary(), OnlyMotor, longDirectory);

            Assert.AreEqual(1, Directory.GetFiles(longDirectory).Length);
        }

        /// <remarks>
        /// A shared library must not take a type over one it holds; refused before anything is added, and the file keeps
        /// what it had. Two backup folders, as the registry gives each write its own and an archive will not overwrite.
        /// </remarks>
        [TestMethod]
        public void PublishLibraryTypes_ATypeTheLibraryHolds_IsRefusedAndChangesNothing()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            var library = NewLibrary();
            var first = AssemblyHooks.SharedPortal.PublishLibraryTypes(library, OnlyMotor, Path.Combine(_testDirectory, "archive-1"));

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PublishLibraryTypes(library, OnlyMotor, Path.Combine(_testDirectory, "archive-2")));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "The global library already holds", System.StringComparison.Ordinal);
            Assert.AreEqual(first["types"]!.ToJsonString(), AssemblyHooks.SharedPortal.GetLibraryTypes(library)["types"]!.ToJsonString());
        }

        [TestMethod]
        public void PublishLibraryTypes_ATypeTheProjectLacks_NamesTheProjectLibrary()
        {
            var library = NewLibrary();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.PublishLibraryTypes(library, NoSuchType, Backup()));

            StringAssert.Contains(failure.Message, "The project library has no type", System.StringComparison.Ordinal);
        }

        /// <remarks><c>Path.IsPathRooted</c> takes this, and a folder was left relative to the current directory: found by the review of 2026-11-02.</remarks>
        [TestMethod]
        public void CreateGlobalLibrary_ADriveRelativeDirectory_IsRefusedAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateGlobalLibrary("C:RelativeToTheDrive", "Kit"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.IsFalse(Directory.Exists("C:RelativeToTheDrive"));
        }

        [TestMethod]
        public void CreateGlobalLibrary_ANameWithASeparator_IsRefusedAsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateGlobalLibrary(Path.Combine(_testDirectory, "libraries"), @"..\Kit"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateGlobalLibrary_ANameAlreadyThere_IsRefused()
        {
            NewLibrary();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateGlobalLibrary(Path.Combine(_testDirectory, "libraries"), "Kit"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreatePlcTypeFromLibrary_AProgramWithoutIt_CreatesItAndASecondCallIsRefused()
        {
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 512-1CK01-0AB0/V2.9", "PLC_2");
            var second = AssemblyHooks.SharedPortal.GetSoftwarePaths("PLC_2").Single();

            var created = AssemblyHooks.SharedPortal.CreatePlcTypeFromLibrary(second, DataType, null);

            Assert.AreEqual(DataType, created["name"]!.GetValue<string>());
            Assert.AreEqual("0.0.1", created["version"]!.GetValue<string>());
            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreatePlcTypeFromLibrary(second, DataType, null));
        }

        /// <remarks>0967_1's PLC holds the data type already, and TIA Portal would answer with it and no error.</remarks>
        [TestMethod]
        public void CreatePlcTypeFromLibrary_AProgramHoldingIt_IsRefused()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreatePlcTypeFromLibrary(PlcPath(), DataType, null));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "already holds", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreatePlcTypeFromLibrary_AFaceplate_IsRefusedAsNotAPlcDataType()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreatePlcTypeFromLibrary(PlcPath(), Motor, null));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        private void PlaceOnVersionOne()
        {
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Motor, null, string.Empty, Backup());
            AssemblyHooks.SharedPortal.PlaceUnifiedFaceplates(HmiPath, UnifiedFaceplateListReader.Parse(@"{ ""screen"": ""Principal"", ""faceplates"": [
                { ""name"": ""FP_M2"", ""type"": ""Faceplate_BOTO_MOTOR_V1"", ""left"": 0, ""top"": 0, ""interface"": { ""Interface_MOTOR"": { ""tag"": ""M2"" } } } ] }"), Backup());
        }

        private static JsonObject Faceplate(JsonObject content)
        {
            return (JsonObject)content["faceplates"]!.AsArray().Single()!;
        }

        private static string PlcPath()
        {
            return AssemblyHooks.SharedPortal.GetSoftwarePaths().Single(path => path != HmiPath);
        }

        private string NewLibrary()
        {
            return AssemblyHooks.SharedPortal.CreateGlobalLibrary(Path.Combine(_testDirectory, "libraries"), "Kit")["library"]!.GetValue<string>();
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup");
        }
    }
}
