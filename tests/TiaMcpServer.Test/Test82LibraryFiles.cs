using System;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>A global library's new versions, archive and retrieval; master copies deleted; the type folder tree.</summary>
    /// <remarks>On the class project 0967_1. Measured 2026-11-24 on TIA Portal V21 by probes on copies of it before building.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test82LibraryFiles
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Block = "FC_Kit";

        private static readonly string[] BlockOnly = { Block };

        private static readonly string[] BothVersions = { "0.0.1", "0.0.2" };

        private static readonly string[] FirstVersionOnly = { "0.0.1" };

        private static readonly string[] NestedFolders = { "Motors/", "Motors/Pumps/" };

        private string _testDirectory = string.Empty;

        private int _folders;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.FaceplateArchivePath, Path.Combine(_testDirectory, "project"));
            AssemblyHooks.SharedPortal.CompileSoftware(PlcPath());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void UpdateGlobalLibraryTypes_ANewReleasedVersion_ReachesTheGlobalLibrary()
        {
            CreateAndRelease("// version one");
            var library = PublishToNewGlobalLibrary();
            NewBlockVersion("// version two");

            Portal.UpdateGlobalLibraryTypes(library, BlockOnly, Backup());

            CollectionAssert.AreEqual(BothVersions, GlobalVersions(library));
        }

        [TestMethod]
        public void UpdateGlobalLibraryTypes_NothingNew_IsRefusedAndWritesNothing()
        {
            CreateAndRelease("// version one");
            var library = PublishToNewGlobalLibrary();

            var failure = Assert.ThrowsException<PortalException>(() => Portal.UpdateGlobalLibraryTypes(library, BlockOnly, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            CollectionAssert.AreEqual(FirstVersionOnly, GlobalVersions(library));
        }

        [TestMethod]
        public void ArchiveGlobalLibrary_ThenRetrieve_GivesBackItsTypes()
        {
            CreateAndRelease("// version one");
            var library = PublishToNewGlobalLibrary();
            var archives = NewFolder();

            var archive = Portal.ArchiveGlobalLibrary(library, archives)["archive"]!.GetValue<string>();
            var retrieved = Portal.RetrieveGlobalLibrary(archive, Path.Combine(NewFolder(), "restored"));

            Assert.IsTrue(File.Exists(archive), archive);
            Assert.AreEqual(Block, retrieved["types"]!.AsArray().Single()!["name"]!.GetValue<string>());
        }

        [TestMethod]
        public void ArchiveGlobalLibrary_AnArchiveOfItsNameThere_IsRefused()
        {
            CreateAndRelease("// version one");
            var library = PublishToNewGlobalLibrary();
            var archives = NewFolder();
            Portal.ArchiveGlobalLibrary(library, archives);

            var failure = Assert.ThrowsException<PortalException>(() => Portal.ArchiveGlobalLibrary(library, archives));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        [TestMethod]
        public void DeleteMasterCopy_ASavedProject_DeletesItAndArchivesTheProjectFirst()
        {
            CreateAndRelease("// version one");
            Portal.CreateMasterCopy(PlcPath(), new Library.MasterCopySource(Library.MasterCopySourceKind.Block, Block), string.Empty, Backup());
            Portal.SaveProject();
            var backup = Backup();

            Portal.DeleteMasterCopy(Block, backup);

            Assert.AreEqual(0, Portal.GetMasterCopies(string.Empty)["masterCopies"]!.AsArray().Count(copy => copy!["name"]!.GetValue<string>() == Block));
            Assert.AreEqual(1, Directory.GetFiles(backup, "*.zap21").Length);
        }

        [TestMethod]
        public void DeleteMasterCopyFolder_AFolderHoldingAMasterCopy_IsRefusedAndKeepsIt()
        {
            CreateAndRelease("// version one");
            Portal.CreateMasterCopyFolder("Motors", Backup());
            Portal.CreateMasterCopy(PlcPath(), new Library.MasterCopySource(Library.MasterCopySourceKind.Block, Block), "Motors", Backup());
            Portal.SaveProject();

            var failure = Assert.ThrowsException<PortalException>(() => Portal.DeleteMasterCopyFolder("Motors", Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(1, Portal.GetMasterCopies(string.Empty)["masterCopies"]!.AsArray().Count);
        }

        [TestMethod]
        public void DeleteMasterCopyFolder_AnEmptyFolder_DeletesIt()
        {
            Portal.CreateMasterCopyFolder("Motors", Backup());
            Portal.SaveProject();

            Portal.DeleteMasterCopyFolder("Motors", Backup());

            Assert.AreEqual(0, Portal.GetMasterCopies(string.Empty)["folders"]!.AsArray().Count);
        }

        [TestMethod]
        public void CreateLibraryTypeFolder_ANestedFolder_IsListedEmpty()
        {
            Portal.CreateLibraryTypeFolder("Motors", Backup());

            var tree = Portal.CreateLibraryTypeFolder("Motors/Pumps", Backup());

            CollectionAssert.IsSubsetOf(NestedFolders, tree["folders"]!.AsArray().Select(folder => folder!.GetValue<string>()).ToArray());
        }

        private static Portal Portal => AssemblyHooks.SharedPortal;

        private void CreateAndRelease(string body)
        {
            Portal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, BlockDocument(body), Block);
            Portal.ReleaseLibraryType(Block, null, string.Empty, Backup());
        }

        private void NewBlockVersion(string body)
        {
            Portal.CreateLibraryTypeVersionFromDocument(new DocumentImportRequest(PlcPath(), string.Empty, BlockDocument(body), Backup()), Block, replaceInWork: false);
            Portal.ReleaseLibraryType(Block, null, string.Empty, Backup());
        }

        private string PublishToNewGlobalLibrary()
        {
            var library = Portal.CreateGlobalLibrary(NewFolder(), "Kit")["library"]!.GetValue<string>();
            Portal.PublishLibraryTypes(library, BlockOnly, Backup());

            return library;
        }

        private static string[] GlobalVersions(string library)
        {
            var type = Portal.GetLibraryTypes(library)["types"]!.AsArray().Single(entry => entry!["name"]!.GetValue<string>() == Block)!;

            return type["versions"]!.AsArray().Select(version => version!["version"]!.GetValue<string>()).OrderBy(number => number, StringComparer.Ordinal).ToArray();
        }

        private string BlockDocument(string body)
        {
            var folder = NewFolder();
            var text = "{\r\n    S7_EditorMode := \"SCL\";\r\n    S7_Optimized := \"TRUE\";\r\n    S7_Version := \"0.1\"\r\n}\r\nFUNCTION \"" + Block + "\" : Void\r\n    { S7_Language := \"SCL\" }\r\n    NETWORK\r\n        " + body + "\r\n    END_NETWORK\r\nEND_FUNCTION\r\n";
            File.WriteAllText(Path.Combine(folder, Block + ".s7dcl"), text, new UTF8Encoding(true));

            return folder;
        }

        private string NewFolder()
        {
            var folder = Path.Combine(_testDirectory, "f", (++_folders).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);

            return folder;
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "b", (++_folders).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string PlcPath()
        {
            return Portal.GetSoftwarePaths().Single(path => path != HmiPath);
        }
    }
}
