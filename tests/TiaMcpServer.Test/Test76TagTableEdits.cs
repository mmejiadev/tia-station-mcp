using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Changing some rows of a PLC tag table and leaving the others, through <c>EditTagTable</c>.</summary>
    /// <remarks>On a copy of the fixture, in a table of its own made with <c>CreateTagTable</c> and <c>CreateTag</c>.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test76TagTableEdits
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string Table = "TiaMcpEdit";

        private static readonly string[] AsCreated = { "Start", "Level" };
        private static readonly string[] StartAndStop = { "Start", "Stop" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
            AssemblyHooks.SharedPortal.CreateTagTable(Software, Table, Backup());
            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(Table + "/Start", "Bool", "%I100.0"), Backup());
            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(Table + "/Level", "Int", "%IW102"), Backup());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void EditTagTable_SetAnAddressAndAComment_ChangesOnlyThat()
        {
            Edit(@"""set"": [ { ""name"": ""Start"", ""address"": ""%I100.1"", ""comment"": ""Start button, NO"" } ]");

            var start = Tag("Start");
            Assert.AreEqual("%I100.1", start.Assignment);
            Assert.AreEqual("Bool", start.DataTypeName);
            Assert.AreEqual("%IW102", Tag("Level").Assignment);
        }

        [TestMethod]
        public void EditTagTable_RemoveAndAdd_LeavesTheOthers()
        {
            Edit(@"""remove"": [ ""Level"" ], ""add"": [ { ""name"": ""Stop"", ""dataType"": ""Bool"", ""address"": ""%I100.2"" } ]");

            CollectionAssert.AreEquivalent(StartAndStop, Names());
        }

        [TestMethod]
        public void EditTagTable_ATagNotInTheTable_ThrowsInvalidParamsAndChangesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""remove"": [ ""Level"" ], ""set"": [ { ""name"": ""Nope"", ""address"": ""%I1.0"" } ]"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            CollectionAssert.AreEquivalent(AsCreated, Names());
        }

        /// <remarks>
        /// A name another table of the program already has, which TIA Portal refuses, after a removal done: the removal is
        /// undone, the tag as it was. Not an address nor an odd name: measured 2026-11-19, TIA Portal takes any text as an
        /// address (the reader checks it instead) and a name holding a quote.
        /// </remarks>
        [TestMethod]
        public void EditTagTable_TiaPortalRefusesAName_UndoesWhatWasDone()
        {
            AssemblyHooks.SharedPortal.CreateTagTable(Software, Table + "_Other", Backup());
            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition(Table + "_Other/Taken", "Bool", "%M100.0"), Backup());

            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""remove"": [ ""Level"" ], ""add"": [ { ""name"": ""Taken"", ""dataType"": ""Bool"", ""address"": ""%Q0.1"" } ]"));

            StringAssert.Contains(failure.Message, "undone", StringComparison.Ordinal);
            CollectionAssert.AreEquivalent(AsCreated, Names());
            Assert.AreEqual("%IW102", Tag("Level").Assignment);
        }

        private void Edit(string body)
        {
            AssemblyHooks.SharedPortal.EditTagTable(Software, TagTableEditReader.Parse(@"{ ""table"": """ + Table + @""", " + body + " }"), Backup());
        }

        private static string[] Names()
        {
            return Tags().Select(tag => tag.Name).ToArray();
        }

        private static TagInfo Tag(string name)
        {
            return Tags().Single(tag => tag.Name == name);
        }

        private static TagInfo[] Tags()
        {
            var table = AssemblyHooks.SharedPortal.GetTagTables(Software).Single(candidate => candidate.Path == Table);

            return AssemblyHooks.SharedPortal.GetTags(Software, table.Path).Where(tag => tag.Kind == TagInfo.TagKind).ToArray();
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
