using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using PlcSoftware = Siemens.Engineering.SW.PlcSoftware;

namespace TiaMcpServer.Test
{
    /// <summary>Library types past their first version: blocks from types, instances moved, versions compared, global libraries kept.</summary>
    /// <remarks>
    /// On the class project 0967_1. What each tool relies on was measured on 2026-11-24 on TIA Portal V21 by probes on copies of
    /// it; most of all, that <c>UpdateProject</c> removed a program's instances instead of updating them, which is why
    /// <c>UpdatePlcLibraryInstances</c> deletes and creates them again.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test80LibraryClosing
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Block = "FC_Kit";

        private const string DataType = "UDT_Kit";

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
        public void CreatePlcBlockFromLibrary_AReleasedBlockType_CreatesItsInstance()
        {
            CreateAndRelease("// version one");
            Portal.DeleteBlock(PlcPath(), Block, Backup());

            var created = Portal.CreatePlcBlockFromLibrary(PlcPath(), Block, null, string.Empty);

            Assert.AreEqual(Block, created["name"]!.GetValue<string>());
            Assert.AreEqual("0.0.1", InstanceVersion(Block));
        }

        /// <remarks>TIA Portal would answer with the block there, or create FC_Kit_1, without a word (measured 2026-11-24).</remarks>
        [TestMethod]
        public void CreatePlcBlockFromLibrary_TheProgramHoldsTheBlock_IsRefusedAndCreatesNothing()
        {
            CreateAndRelease("// version one");

            var failure = Assert.ThrowsException<PortalException>(() => Portal.CreatePlcBlockFromLibrary(PlcPath(), Block, null, string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(1, Portal.GetBlocks(PlcPath(), $"^{Block}").Count);
        }

        [TestMethod]
        public void CreatePlcBlockFromLibrary_ADataType_IsRefusedAsInvalidParams()
        {
            CreateDataType("Done : Bool;");

            var failure = Assert.ThrowsException<PortalException>(() => Portal.CreatePlcBlockFromLibrary(PlcPath(), DataType, null, string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        [TestMethod]
        public void EditLibraryType_ABlockTypeThroughItsInstance_StartsTheNextVersionOnIt()
        {
            CreateAndRelease("// version one");

            var type = Portal.EditLibraryType(Block, PlcPath(), Backup());

            Assert.AreEqual("0.0.2", InWork(type));
            Assert.AreEqual("0.0.2", InstanceVersion(Block));
        }

        /// <remarks>TIA Portal V21 refuses a new version of a block type with no instance in the project (measured 2026-11-24).</remarks>
        [TestMethod]
        public void EditLibraryType_ABlockTypeWithNoInstance_IsRefusedAndStartsNothing()
        {
            CreateAndRelease("// version one");
            Portal.DeleteBlock(PlcPath(), Block, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => Portal.EditLibraryType(Block, PlcPath(), Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "CreatePlcBlockFromLibrary", StringComparison.Ordinal);
            Assert.AreEqual(1, Versions(Block).Count);
        }

        [TestMethod]
        public void UpdatePlcLibraryInstances_ABlockOnTheOldVersion_MovesItWithTheNewContent()
        {
            CreateTwoVersions();
            PutBlockOnVersionOne();

            var moved = Portal.UpdatePlcLibraryInstances(PlcPath(), Block, null, Backup());

            Assert.AreEqual(1, moved["moved"]!.AsArray().Count);
            Assert.AreEqual("0.0.2", InstanceVersion(Block));
            StringAssert.Contains(BlockText(), "version two", StringComparison.Ordinal);
        }

        [TestMethod]
        public void UpdatePlcLibraryInstances_EveryInstanceUpToDate_ChangesNothing()
        {
            CreateTwoVersions();

            var moved = Portal.UpdatePlcLibraryInstances(PlcPath(), Block, null, Backup());

            Assert.AreEqual(0, moved["moved"]!.AsArray().Count);
        }

        [TestMethod]
        public void UpdatePlcLibraryInstances_ADataTypeOnTheOldVersion_MovesIt()
        {
            CreateDataType("Done : Bool;");
            NewDataTypeVersion("Done : Bool;\r\n        Extra : Int;");
            DeleteDataTypeInstance();
            Portal.CreatePlcTypeFromLibrary(PlcPath(), DataType, new Version(0, 0, 1));

            Portal.UpdatePlcLibraryInstances(PlcPath(), DataType, null, Backup());

            Assert.AreEqual("0.0.2", InstanceVersion(DataType));
        }

        /// <remarks>A block reading a member the new version drops stops compiling; the move is undone.</remarks>
        [TestMethod]
        public void UpdatePlcLibraryInstances_AVersionThatBreaksAUser_PutsTheInstanceBack()
        {
            CreateDataType("Done : Bool;");
            NewDataTypeVersion("Extra : Int;");
            DeleteDataTypeInstance();
            Portal.CreatePlcTypeFromLibrary(PlcPath(), DataType, new Version(0, 0, 1));
            Portal.WriteScl(PlcPath(), "FUNCTION \"FC_UsesKit\" : Void\r\nVAR_TEMP\r\n    t : \"" + DataType + "\";\r\nEND_VAR\r\nBEGIN\r\n    #t.Done := TRUE;\r\nEND_FUNCTION\r\n", Backup());

            var failure = Assert.ThrowsException<PortalException>(() => Portal.UpdatePlcLibraryInstances(PlcPath(), DataType, null, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "back on the version it showed", StringComparison.Ordinal);
            Assert.AreEqual("0.0.1", InstanceVersion(DataType));
        }

        [TestMethod]
        public void CompareLibraryTypeVersions_TwoVersionsOfABlock_FindsTheChangedLine()
        {
            CreateTwoVersions();

            var compared = Portal.CompareLibraryTypeVersions(Block, new Version(0, 0, 1), new Version(0, 0, 2), string.Empty);

            Assert.IsFalse(compared["contentIdentical"]!.GetValue<bool>());
            var document = compared["documents"]!.AsArray().Single()!;
            Assert.AreEqual("different", document["state"]!.GetValue<string>());
            StringAssert.Contains(document["otherText"]!.GetValue<string>(), "version two", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CompareLibraryTypeVersions_AVersionWithItself_IsIdentical()
        {
            CreateAndRelease("// version one");

            var compared = Portal.CompareLibraryTypeVersions(Block, new Version(0, 0, 1), new Version(0, 0, 1), string.Empty);

            Assert.IsTrue(compared["contentIdentical"]!.GetValue<bool>());
        }

        [TestMethod]
        public void CompareLibraryTypeVersions_AVersionThatIsNotThere_IsRefusedAsNotFound()
        {
            CreateAndRelease("// version one");

            var failure = Assert.ThrowsException<PortalException>(() => Portal.CompareLibraryTypeVersions(Block, new Version(0, 0, 1), new Version(0, 0, 9), string.Empty));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
        }

        private static Portal Portal => AssemblyHooks.SharedPortal;

        private void CreateAndRelease(string body)
        {
            Portal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, BlockDocument(body), Block);
            Portal.ReleaseLibraryType(Block, null, string.Empty, Backup());
        }

        private void CreateTwoVersions()
        {
            CreateAndRelease("// version one");
            NewBlockVersion("// version two");
        }

        private void NewBlockVersion(string body)
        {
            Portal.CreateLibraryTypeVersionFromDocument(new DocumentImportRequest(PlcPath(), string.Empty, BlockDocument(body), Backup()), Block, replaceInWork: false);
            Portal.ReleaseLibraryType(Block, null, string.Empty, Backup());
        }

        private void PutBlockOnVersionOne()
        {
            Portal.DeleteBlock(PlcPath(), Block, Backup());
            Portal.CreatePlcBlockFromLibrary(PlcPath(), Block, new Version(0, 0, 1), string.Empty);
            Portal.CompileSoftware(PlcPath());
        }

        private void CreateDataType(string members)
        {
            Portal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, DataTypeDocument(members), DataType);
            Portal.ReleaseLibraryType(DataType, null, string.Empty, Backup());
        }

        private void NewDataTypeVersion(string members)
        {
            Portal.CreateLibraryTypeVersionFromDocument(new DocumentImportRequest(PlcPath(), string.Empty, DataTypeDocument(members), Backup()), DataType, replaceInWork: false);
            Portal.ReleaseLibraryType(DataType, null, string.Empty, Backup());
        }

        /// <remarks>No tool deletes a PLC data type, so the instance is deleted through Openness, by the server's own lookup.</remarks>
        private static void DeleteDataTypeInstance()
        {
            var lookup = typeof(Portal).GetMethod("RequireSoftware", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new AssertFailedException("Portal.RequireSoftware is gone: this test reaches the program through it.");
            var software = (PlcSoftware)lookup.Invoke(Portal, new object[] { PlcPath() })!;

            software.TypeGroup.Types.Find(DataType).Delete();
        }

        private static string? InstanceVersion(string typeName)
        {
            return Portal.GetLibraryTypeUsage()["usages"]!.AsArray()
                .SingleOrDefault(usage => usage!["type"]!.GetValue<string>() == typeName)?["instanceVersion"]?.GetValue<string>();
        }

        private static JsonArray Versions(string typeName)
        {
            return Portal.GetLibraryTypes(string.Empty)["types"]!.AsArray().Single(type => type!["name"]!.GetValue<string>() == typeName)!["versions"]!.AsArray();
        }

        private static string InWork(JsonObject type)
        {
            return type["versions"]!.AsArray().Single(version => version!["state"]!.GetValue<string>() == "inWork")!["version"]!.GetValue<string>();
        }

        private string BlockText()
        {
            var export = NewFolder();
            Portal.ExportAsDocuments(PlcPath(), Block, export);

            return File.ReadAllText(Directory.GetFiles(export, "*.s7dcl", SearchOption.AllDirectories).Single());
        }

        private string BlockDocument(string body)
        {
            var folder = NewFolder();
            var text = "{\r\n    S7_EditorMode := \"SCL\";\r\n    S7_Optimized := \"TRUE\";\r\n    S7_Version := \"0.1\"\r\n}\r\nFUNCTION \"" + Block + "\" : Void\r\n    { S7_Language := \"SCL\" }\r\n    NETWORK\r\n        " + body + "\r\n    END_NETWORK\r\nEND_FUNCTION\r\n";
            File.WriteAllText(Path.Combine(folder, Block + ".s7dcl"), text, new UTF8Encoding(true));

            return folder;
        }

        private string DataTypeDocument(string members)
        {
            var folder = NewFolder();
            File.WriteAllText(Path.Combine(folder, DataType + ".s7dcl"), "TYPE\r\n    " + DataType + " : STRUCT\r\n        " + members + "\r\n    END_STRUCT;\r\nEND_TYPE\r\n", new UTF8Encoding(true));

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
