using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>Changing some items of a WinCC Unified screen and leaving the others, through <c>EditUnifiedScreen</c>.</summary>
    /// <remarks>On a PC station with WinCC Unified PC RT, as <c>Test44UnifiedScreens</c> builds it.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test74UnifiedScreenEdits
    {
        private const string StationName = "PC-System_1";
        private const string HmiPath = "PC-System_1/HMI_RT_1";
        private const string ScreenName = "Edited";

        private const string Start = @"{ ""name"": """ + ScreenName + @""", ""items"": [
            { ""type"": ""rectangle"", ""name"": ""Tank"", ""properties"": { ""Left"": 40, ""Top"": 60, ""Width"": 120, ""Height"": 200 } },
            { ""type"": ""text"", ""name"": ""Title"", ""properties"": { ""Left"": 40, ""Top"": 10, ""Text"": ""Tank 1"" } },
            { ""type"": ""circle"", ""name"": ""Lamp"", ""properties"": { ""CenterX"": 220, ""CenterY"": 80, ""Radius"": 20 } } ] }";

        private const string EmptyRichText = "<body><p/></body>";

        private static readonly string[] AsDrawn = { "Tank", "Title", "Lamp" };
        private static readonly string[] WithGo = { "Tank", "Title", "Lamp", "Go" };
        private static readonly string[] WithoutTitle = { "Tank", "Lamp" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedScreenEdits");
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(Start), Backup());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void EditUnifiedScreen_DrawANewItem_AddsItOnTopAndKeepsTheOthers()
        {
            Edit(@"""draw"": [ { ""type"": ""button"", ""name"": ""Go"", ""properties"": { ""Left"": 40, ""Top"": 300, ""Text"": ""Go"" } } ]");

            CollectionAssert.AreEqual(WithGo, Names());
            Assert.AreEqual(120, Property("Tank", "Width"));
        }

        [TestMethod]
        public void EditUnifiedScreen_SetAProperty_ChangesItInPlace()
        {
            Edit(@"""set"": [ { ""name"": ""Tank"", ""properties"": { ""Left"": 300 } } ]");

            Assert.AreEqual(300, Property("Tank", "Left"));
            Assert.AreEqual(200, Property("Tank", "Height"));
            CollectionAssert.AreEqual(AsDrawn, Names());
        }

        [TestMethod]
        public void EditUnifiedScreen_RemoveAnItem_DeletesOnlyIt()
        {
            Edit(@"""remove"": [ ""Title"" ]");

            CollectionAssert.AreEqual(WithoutTitle, Names());
        }

        [TestMethod]
        public void EditUnifiedScreen_DrawUnderAnItemsName_ReplacesIt()
        {
            Edit(@"""draw"": [ { ""type"": ""ellipse"", ""name"": ""Tank"", ""properties"": { ""CenterX"": 100, ""CenterY"": 160 } } ]");

            var tank = Item("Tank");
            Assert.AreEqual("ellipse", tank["type"]!.GetValue<string>());
            Assert.AreEqual(3, Names().Length);
        }

        [TestMethod]
        public void EditUnifiedScreen_ItemThatIsNotThere_ThrowsInvalidParamsAndChangesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""remove"": [ ""Lamp"" ], ""set"": [ { ""name"": ""Pump"", ""properties"": { ""Left"": 1 } } ]"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            CollectionAssert.AreEqual(AsDrawn, Names());
        }

        [TestMethod]
        public void EditUnifiedScreen_ABrokenScript_IsRefusedBeforeAnythingChanges()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""remove"": [ ""Lamp"" ],
                ""draw"": [ { ""type"": ""button"", ""name"": ""Go"", ""events"": [ { ""event"": ""tapped"", ""script"": ""HMIRuntime.Trace("" } ] } ]"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            CollectionAssert.AreEqual(AsDrawn, Names());
        }

        /// <remarks>
        /// A name TIA Portal refuses only when the item is created, after the removal before it was done: the removal is
        /// undone from the backup, the item drawn back on top.
        /// </remarks>
        [TestMethod]
        public void EditUnifiedScreen_TiaPortalRefusesPartOfIt_UndoesWhatWasDone()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""remove"": [ ""Title"" ],
                ""draw"": [ { ""type"": ""button"", ""name"": ""Bad/Name\""&?"", ""properties"": { ""Left"": 40 } } ]"));

            StringAssert.Contains(failure.Message, "undone", StringComparison.Ordinal);
            CollectionAssert.AreEquivalent(AsDrawn, Names());
            Assert.AreEqual("Tank 1", Item("Title")["properties"]!["Text"]!.GetValue<string>());
        }

        /// <remarks>Review of 2026-11-17: an item only set in place gets its value back and keeps its place, not drawn again on top.</remarks>
        [TestMethod]
        public void EditUnifiedScreen_RefusedAfterASet_PutsTheValueBackInPlace()
        {
            Assert.ThrowsException<PortalException>(() => Edit(@"""set"": [ { ""name"": ""Tank"", ""properties"": { ""Left"": 300 } } ],
                ""draw"": [ { ""type"": ""button"", ""name"": ""Bad/Name\""&?"" } ]"));

            Assert.AreEqual(40, Property("Tank", "Left"));
            CollectionAssert.AreEqual(AsDrawn, Names());
        }

        /// <remarks>Review of 2026-11-17: a value set on a property a tag drives would change nothing in runtime, and say nothing.</remarks>
        [TestMethod]
        public void EditUnifiedScreen_SetAPropertyATagDrives_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Pos"", ""dataType"": ""Int"" } ] }"));
            Edit(@"""draw"": [ { ""type"": ""rectangle"", ""name"": ""Tank"", ""properties"": { ""Top"": 60 }, ""tags"": { ""Left"": ""Pos"" } } ]");

            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""set"": [ { ""name"": ""Tank"", ""properties"": { ""Left"": 300 } } ]"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>
        /// Review of 2026-11-17: a text not there yet is not read, and is put back as empty rather than refusing the set.
        /// Measured 2026-11-17 on V21: an empty tooltip written back reads as the rich text <c>&lt;body&gt;&lt;p/&gt;&lt;/body&gt;</c>.
        /// </remarks>
        [TestMethod]
        public void EditUnifiedScreen_RefusedAfterSettingAnEmptyText_PutsItBackEmpty()
        {
            Assert.ThrowsException<PortalException>(() => Edit(@"""set"": [ { ""name"": ""Tank"", ""properties"": { ""ToolTipText"": ""Water"" } } ],
                ""draw"": [ { ""type"": ""button"", ""name"": ""Bad/Name\""&?"" } ]"));

            var tip = Item("Tank")["properties"]!["ToolTipText"];
            Assert.IsTrue(tip == null || tip.GetValue<string>() is "" or EmptyRichText, $"ToolTipText is '{tip}'");
        }

        [TestMethod]
        public void EditUnifiedScreen_AnEdit_LeavesTheTouchedItemsInTheBackup()
        {
            var edited = Edit(@"""remove"": [ ""Title"" ]");

            var backup = JsonNode.Parse(File.ReadAllText(edited["backup"]!.GetValue<string>()))!;
            Assert.AreEqual("Title", backup["items"]![0]!["name"]!.GetValue<string>());
        }

        private JsonObject Edit(string body)
        {
            var edit = UnifiedScreenEditReader.Parse(@"{ ""screen"": """ + ScreenName + @""", " + body + " }");

            return AssemblyHooks.SharedPortal.EditUnifiedScreen(HmiPath, edit, Backup());
        }

        private static string[] Names()
        {
            return Screen()["items"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ToArray();
        }

        private static JsonNode Item(string name)
        {
            return Screen()["items"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == name)!;
        }

        private static int Property(string item, string property)
        {
            return Item(item)["properties"]![property]!.GetValue<int>();
        }

        private static JsonObject Screen()
        {
            return AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, ScreenName);
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
