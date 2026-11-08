using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Master copies of the project library: folders, making one from a block or a tag table, and creating from one (ROADMAP H6).</summary>
    /// <remarks>
    /// On the class project 0967_1, with a function written into its PLC so the block's name is known. What a master copy's
    /// content description says, and what its name is, are measured here rather than assumed.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test59MasterCopies
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Function = "FC_MasterCopySource";

        private const string FunctionScl = "FUNCTION \"" + Function + "\" : Void\r\nBEGIN\r\nEND_FUNCTION\r\n";

        private const string DataType = "UDT_MasterCopySource";

        private const string Table = "MC_Table";

        private const string TableTag = "MC_Running";

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
        public void GetMasterCopies_TheProjectLibrary_NamesItAndListsFoldersAndMasterCopies()
        {
            var read = AssemblyHooks.SharedPortal.GetMasterCopies(string.Empty);

            Assert.AreEqual("project", read["library"]!.GetValue<string>());
            Assert.IsNotNull(read["folders"] as JsonArray);
            Assert.IsNotNull(read["masterCopies"] as JsonArray);
        }

        [TestMethod]
        public void GetMasterCopies_ANewGlobalLibrary_HoldsNone()
        {
            var library = AssemblyHooks.SharedPortal.CreateGlobalLibrary(Path.Combine(_testDirectory, "libraries"), "Kit")["library"]!.GetValue<string>();

            var read = AssemblyHooks.SharedPortal.GetMasterCopies(library);

            Assert.AreEqual(0, read["masterCopies"]!.AsArray().Count);
        }

        [TestMethod]
        public void CreateMasterCopyFolder_ANestedFolder_IsListed()
        {
            AssemblyHooks.SharedPortal.CreateMasterCopyFolder("Kit", Backup());

            var read = AssemblyHooks.SharedPortal.CreateMasterCopyFolder("Kit/Motors", Backup());

            CollectionAssert.Contains(Folders(read), "Kit/Motors");
        }

        [TestMethod]
        public void CreateMasterCopyFolder_AFolderAlreadyThere_IsRefusedAsInvalidState()
        {
            AssemblyHooks.SharedPortal.CreateMasterCopyFolder("Kit", Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateMasterCopyFolder("kit", Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateMasterCopyFolder_UnderAMissingFolder_IsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateMasterCopyFolder("NoSuchFolder/Motors", Backup()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
        }

        /// <remarks>Measures what a block's master copy is named and what its content description says.</remarks>
        [TestMethod]
        public void CreateMasterCopy_AFunction_IsNamedAfterItAndHoldsOneBlock()
        {
            WriteFunction();

            var created = AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, Function), string.Empty, Backup());

            Assert.AreEqual(Function, created["name"]!.GetValue<string>());
            var content = created["contents"]!.AsArray().Single()!;
            Assert.AreEqual("block", content["kind"]!.GetValue<string>(), content.ToJsonString());
        }

        [TestMethod]
        public void CreateMasterCopy_TheDefaultTagTable_HoldsOneTagTable()
        {
            var created = AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.TagTable, DefaultTagTable()), string.Empty, Backup());

            var content = created["contents"]!.AsArray().Single()!;
            Assert.AreEqual("tagTable", content["kind"]!.GetValue<string>(), content.ToJsonString());
        }

        [TestMethod]
        public void CreateMasterCopy_TheSameBlockTwice_IsRefusedAsInvalidState()
        {
            WriteFunction();
            var source = new MasterCopySource(MasterCopySourceKind.Block, Function);
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), source, string.Empty, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), source, string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateMasterCopy_ANamePattern_IsRefusedAsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, "FC_.*"), string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateMasterCopy_AMissingBlock_IsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, "FC_NoSuchBlock"), string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateFromMasterCopy_AFunctionIntoAnotherProgram_CreatesIt()
        {
            WriteFunction();
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, Function), string.Empty, Backup());
            var second = AddSecondPlc();

            var created = AssemblyHooks.SharedPortal.CreateFromMasterCopy(second, Function, string.Empty, Backup());

            Assert.AreEqual(Function, created["name"]!.GetValue<string>());
            Assert.AreEqual("block", created["kind"]!.GetValue<string>());
            Assert.IsNotNull(AssemblyHooks.SharedPortal.GetBlock(second, Function));
        }

        /// <remarks>TIA Portal's default would have created <c>FC_MasterCopySource_1</c> without a word (measured 2026-11-01).</remarks>
        [TestMethod]
        public void CreateFromMasterCopy_AProgramHoldingTheBlock_IsRefusedAndRenamesNothing()
        {
            WriteFunction();
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, Function), string.Empty, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateFromMasterCopy(PlcPath(), Function, string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.IsNull(AssemblyHooks.SharedPortal.GetBlock(PlcPath(), Function + "_1"));
        }

        [TestMethod]
        public void CreateFromMasterCopy_TheDefaultTagTableIntoAProgramWithItsOwn_IsRefused()
        {
            var table = DefaultTagTable();
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.TagTable, table), string.Empty, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateFromMasterCopy(PlcPath(), table, string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateFromMasterCopy_NoSuchMasterCopy_IsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateFromMasterCopy(PlcPath(), "NoSuchMasterCopy", string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
        }

        [TestMethod]
        public void CreateFromMasterCopy_AMissingGroup_IsNotFoundAndCreatesNothing()
        {
            WriteFunction();
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, Function), string.Empty, Backup());
            var second = AddSecondPlc();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateFromMasterCopy(second, Function, "NoSuchGroup", Backup()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
            Assert.IsNull(AssemblyHooks.SharedPortal.GetBlock(second, Function));
        }

        [TestMethod]
        public void CreateMasterCopy_AFolderSpelledInAnotherCase_GivesTheFoldersOwnName()
        {
            WriteFunction();
            AssemblyHooks.SharedPortal.CreateMasterCopyFolder("Kit", Backup());

            var created = AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, Function), "kit", Backup());

            Assert.AreEqual("Kit/" + Function, created["path"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateFromMasterCopy_APlcDataTypeIntoAnotherProgram_CreatesIt()
        {
            AssemblyHooks.SharedPortal.WriteScl(PlcPath(), DataTypeScl(DataType), Backup());
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.PlcType, DataType), string.Empty, Backup());
            var second = AddSecondPlc();

            var created = AssemblyHooks.SharedPortal.CreateFromMasterCopy(second, DataType, string.Empty, Backup());

            Assert.AreEqual("plcType", created["kind"]!.GetValue<string>());
            Assert.IsNotNull(AssemblyHooks.SharedPortal.GetType(second, DataType));
        }

        [TestMethod]
        public void CreateFromMasterCopy_ATagTableIntoAnotherProgram_CreatesItWithItsTag()
        {
            MakeTableMasterCopy();
            var second = AddSecondPlc();

            var created = AssemblyHooks.SharedPortal.CreateFromMasterCopy(second, Table, string.Empty, Backup());

            Assert.AreEqual("tagTable", created["kind"]!.GetValue<string>());
            Assert.IsTrue(AssemblyHooks.SharedPortal.GetTags(second, Table).Any(tag => tag.Name == TableTag));
        }

        /// <remarks>
        /// A tag table's tags are not in its content description, and TIA Portal renamed one the program held without a word
        /// (MC_Running became MC_Running(1), measured 2026-11-08 by this test). Refused, and the table taken back.
        /// </remarks>
        [TestMethod]
        public void CreateFromMasterCopy_ATagTableBringingATagTheProgramHolds_IsRefusedAndLeavesNoTable()
        {
            MakeTableMasterCopy();
            var second = AddSecondPlc();
            AssemblyHooks.SharedPortal.CreateTag(second, new TagDefinition($"{DefaultTagTable(second)}/{TableTag}", "Bool", "%M101.0"), Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateFromMasterCopy(second, Table, string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, TableTag, System.StringComparison.Ordinal);
            Assert.IsFalse(AssemblyHooks.SharedPortal.GetTagTables(second).Any(table => table.Path == Table));
        }

        /// <remarks>A block and a PLC data type may not share a name in a program.</remarks>
        [TestMethod]
        public void CreateFromMasterCopy_ABlockNamedLikeADataTypeOfTheProgram_IsRefused()
        {
            WriteFunction();
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.Block, Function), string.Empty, Backup());
            var second = AddSecondPlc();
            AssemblyHooks.SharedPortal.WriteScl(second, DataTypeScl(Function), Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateFromMasterCopy(second, Function, string.Empty, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        private void WriteFunction()
        {
            AssemblyHooks.SharedPortal.WriteScl(PlcPath(), FunctionScl, Backup());
            AssemblyHooks.SharedPortal.CompileSoftware(PlcPath());
        }

        private void MakeTableMasterCopy()
        {
            AssemblyHooks.SharedPortal.CreateTagTable(PlcPath(), Table, Backup());
            AssemblyHooks.SharedPortal.CreateTag(PlcPath(), new TagDefinition($"{Table}/{TableTag}", "Bool", "%M100.0"), Backup());
            AssemblyHooks.SharedPortal.CreateMasterCopy(PlcPath(), new MasterCopySource(MasterCopySourceKind.TagTable, Table), string.Empty, Backup());
        }

        private static string DataTypeScl(string name)
        {
            return "TYPE \"" + name + "\"\r\nVERSION : 0.1\r\n   STRUCT\r\n      Running : Bool;\r\n   END_STRUCT;\r\n\r\nEND_TYPE\r\n";
        }

        /// <remarks>
        /// Compiled at once: a new device's Main has never compiled, and TIA Portal V20 exports it neither as SimaticML nor
        /// as a document, so the backup every write takes first refuses the write (measured 2026-11-08).
        /// </remarks>
        private static string AddSecondPlc()
        {
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 512-1CK01-0AB0/V2.9", "PLC_2");
            var second = AssemblyHooks.SharedPortal.GetSoftwarePaths("PLC_2").Single();
            AssemblyHooks.SharedPortal.CompileSoftware(second);

            return second;
        }

        private static string DefaultTagTable()
        {
            return DefaultTagTable(PlcPath());
        }

        private static string DefaultTagTable(string softwarePath)
        {
            return AssemblyHooks.SharedPortal.GetTagTables(softwarePath).Single(table => table.IsDefault).Path;
        }

        private static string[] Folders(JsonObject read)
        {
            return read["folders"]!.AsArray().Select(folder => folder!.GetValue<string>()).ToArray();
        }

        private static string PlcPath()
        {
            return AssemblyHooks.SharedPortal.GetSoftwarePaths().Single(path => path != HmiPath);
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup");
        }
    }
}
