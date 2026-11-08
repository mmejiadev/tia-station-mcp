using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Library types of blocks and PLC data types, and their new versions, from SIMATIC SD documents.</summary>
    /// <remarks>On the class project 0967_1. What each refusal stands for was measured on 2026-11-08 by probes on a copy.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test61LibraryTypesFromDocuments
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Block = "FC_TypeFromDocument";

        private const string DataType = "UDT_TypeFromDocument";

        private string _testDirectory = string.Empty;

        private int _documents;

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
        public void CreateLibraryTypeFromDocument_AFunction_MakesTheTypeInWorkAndItsInstance()
        {
            var type = Create();

            Assert.AreEqual("inWork", SingleVersion(type)["state"]!.GetValue<string>());
            Assert.AreEqual(1, AssemblyHooks.SharedPortal.GetBlocks(PlcPath(), $"^{Block}$").Count);
        }

        [TestMethod]
        public void CreateLibraryTypeFromDocument_ADataType_MakesTheTypeAndItsInstance()
        {
            var type = AssemblyHooks.SharedPortal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, DataTypeDocument("Done : Bool;"), DataType);

            Assert.AreEqual("inWork", SingleVersion(type)["state"]!.GetValue<string>());
            Assert.AreEqual(1, AssemblyHooks.SharedPortal.GetTypes(PlcPath(), $"^{DataType}$").Count);
        }

        /// <remarks>TIA Portal takes this and only refuses it at the release (measured 2026-11-08).</remarks>
        [TestMethod]
        public void CreateLibraryTypeFromDocument_ADocumentThatDoesNotCompile_IsRefusedAndLeavesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, BlockDocument("NOT SCL AT ALL +;"), Block));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "does not compile", System.StringComparison.Ordinal);
            Assert.IsFalse(HoldsType(), "the type stayed");
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetBlocks(PlcPath(), $"^{Block}$").Count);
        }

        [TestMethod]
        public void CreateLibraryTypeFromDocument_ANameTheProgramHolds_IsRefused()
        {
            Create();
            var second = BlockDocument("// again");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, second, Block));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateLibraryTypeFromDocument_AGroupThatIsNotThere_IsRefusedAndLeavesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateLibraryTypeFromDocument(PlcPath(), "NoSuchGroup", BlockDocument("// one"), Block));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
            Assert.IsFalse(HoldsType(), "the type was created");
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_AReleasedType_MakesTheNextVersionAndMovesTheInstance()
        {
            CreateAndRelease();

            var type = NewVersion(BlockDocument("// version two"));

            Assert.AreEqual("0.0.2", InWork(type)["version"]!.GetValue<string>());
            StringAssert.Contains(ProgramBlockText(), "version two", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_ADataType_MakesTheNextVersion()
        {
            AssemblyHooks.SharedPortal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, DataTypeDocument("Done : Bool;"), DataType);
            AssemblyHooks.SharedPortal.ReleaseLibraryType(DataType, null, string.Empty, Backup());

            var type = AssemblyHooks.SharedPortal.CreateLibraryTypeVersionFromDocument(new DocumentImportRequest(PlcPath(), string.Empty, DataTypeDocument("Done : Bool;\r\n        Extra : Int;"), Backup()), DataType, replaceInWork: false);

            Assert.AreEqual("0.0.2", InWork(type)["version"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_ADocumentThatDoesNotCompile_PutsTheInstanceBack()
        {
            CreateAndRelease();
            var broken = BlockDocument("NOT SCL AT ALL +;");

            var failure = Assert.ThrowsException<PortalException>(() => NewVersion(broken));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "the instance is back on the one before", System.StringComparison.Ordinal);
            Assert.AreEqual(1, Versions().Count);
            StringAssert.Contains(ProgramBlockText(), "version one", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_AVersionInWork_IsRefusedAndKeepsItsContent()
        {
            Create();
            var other = BlockDocument("// other");

            var failure = Assert.ThrowsException<PortalException>(() => NewVersion(other));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(ProgramBlockText(), "version one", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_ReplaceInWork_ReplacesItsContent()
        {
            Create();

            var type = NewVersion(BlockDocument("// replaced"), replaceInWork: true);

            Assert.AreEqual("0.0.1", SingleVersion(type)["version"]!.GetValue<string>());
            StringAssert.Contains(ProgramBlockText(), "replaced", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_ReplacingWithWhatDoesNotCompile_KeepsWhatItHeld()
        {
            Create();
            var broken = BlockDocument("NOT SCL AT ALL +;");

            var failure = Assert.ThrowsException<PortalException>(() => NewVersion(broken, replaceInWork: true));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "holds what it held", System.StringComparison.Ordinal);
            StringAssert.Contains(ProgramBlockText(), "version one", System.StringComparison.Ordinal);
        }

        /// <remarks>TIA Portal makes a new version through an instance (measured 2026-11-08).</remarks>
        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_NoInstanceInTheProgram_IsRefusedAndMakesNoVersion()
        {
            CreateAndRelease();
            AssemblyHooks.SharedPortal.DeleteBlock(PlcPath(), Block, Backup());
            var document = BlockDocument("// version two");

            var failure = Assert.ThrowsException<PortalException>(() => NewVersion(document));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(1, Versions().Count);
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_ADocumentOfTheOtherKind_IsRefused()
        {
            CreateAndRelease();
            var folder = NewFolder();
            File.WriteAllText(Path.Combine(folder, Block + ".s7dcl"), "TYPE\r\n    " + Block + " : STRUCT\r\n        Done : Bool;\r\n    END_STRUCT;\r\nEND_TYPE\r\n", new UTF8Encoding(true));

            var failure = Assert.ThrowsException<PortalException>(() => NewVersion(folder));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateLibraryTypeVersionFromDocument_AWrite_KeepsTheProgramAndTheDocumentInTheBackup()
        {
            CreateAndRelease();
            var backup = Backup();

            AssemblyHooks.SharedPortal.CreateLibraryTypeVersionFromDocument(new DocumentImportRequest(PlcPath(), string.Empty, BlockDocument("// version two"), backup), Block, replaceInWork: false);

            Assert.IsTrue(Directory.GetFiles(Path.Combine(backup, "Program blocks"), Block + ".*").Length > 0, "no copy of the block");
            StringAssert.Contains(File.ReadAllText(Path.Combine(backup, "document-before", Block + ".s7dcl")), "version one", System.StringComparison.Ordinal);
        }

        private JsonObject Create()
        {
            return AssemblyHooks.SharedPortal.CreateLibraryTypeFromDocument(PlcPath(), string.Empty, BlockDocument("// version one"), Block);
        }

        private void CreateAndRelease()
        {
            Create();
            AssemblyHooks.SharedPortal.ReleaseLibraryType(Block, null, string.Empty, Backup());
        }

        private JsonObject NewVersion(string folder, bool replaceInWork = false)
        {
            return AssemblyHooks.SharedPortal.CreateLibraryTypeVersionFromDocument(new DocumentImportRequest(PlcPath(), string.Empty, folder, Backup()), Block, replaceInWork);
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
            var folder = Path.Combine(_testDirectory, "documents", (++_documents).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);

            return folder;
        }

        private string ProgramBlockText()
        {
            var export = Path.Combine(_testDirectory, "exports", (++_documents).ToString(System.Globalization.CultureInfo.InvariantCulture));
            AssemblyHooks.SharedPortal.ExportAsDocuments(PlcPath(), Block, export);

            return File.ReadAllText(Directory.GetFiles(export, "*.s7dcl", SearchOption.AllDirectories).Single());
        }

        private static JsonNode SingleVersion(JsonObject type)
        {
            return type["versions"]!.AsArray().Single()!;
        }

        private static JsonNode InWork(JsonObject type)
        {
            return type["versions"]!.AsArray().Single(version => version!["state"]!.GetValue<string>() == "inWork")!;
        }

        private static JsonNode? FindType()
        {
            return AssemblyHooks.SharedPortal.GetLibraryTypes(string.Empty)["types"]!.AsArray().SingleOrDefault(type => type!["name"]!.GetValue<string>() == Block);
        }

        private static bool HoldsType()
        {
            return FindType() != null;
        }

        private static JsonArray Versions()
        {
            return FindType()!["versions"]!.AsArray();
        }

        private static string PlcPath()
        {
            return AssemblyHooks.SharedPortal.GetSoftwarePaths().Single(path => path != HmiPath);
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backups", (++_documents).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
