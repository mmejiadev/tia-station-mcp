using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;
using TiaMcpServer.UnifiedTextLists;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// WinCC Unified text lists, which Openness V20 offers only as YAML files to import and export, and
    /// the screen items that show them (HMI-COVERAGE row A9).
    /// </summary>
    /// <remarks>
    /// TIA Portal says an import succeeded when it kept nothing (a list holding a range of values comes in
    /// empty), so every write is read back. The undo is driven here with such a list, handed to the
    /// portal directly: the JSON reader refuses ranges before they get that far.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test49UnifiedTextLists
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string States = @"{ ""name"": ""States"", ""entries"": [ { ""value"": 0, ""text"": ""Stopped"" }, { ""value"": 1, ""text"": ""Running"" }, { ""value"": 2, ""text"": ""It's a fault: 'Ñ'"" } ] }";

        private const string Shown = @"{ ""name"": ""Drawn"", ""startScreen"": true, ""items"": [
            { ""type"": ""symbolicIOField"", ""name"": ""StateField"", ""properties"": { ""ResourceList"": ""States"" }, ""tags"": { ""ProcessValue"": ""State"" } },
            { ""type"": ""text"", ""name"": ""StateText"", ""properties"": { ""Top"": 60 }, ""animations"": [ { ""property"": ""Text"", ""tag"": ""State"", ""list"": ""States"" } ] } ] }";

        /// <summary>See <c>Test48UnifiedProcessDisplays</c>: what a Unified PC station compiles with once it has a start screen.</summary>
        private static readonly string[] EncryptedTransferPasswordErrors =
        {
            "The entered password confirmation does not match the password.",
            "The entered password does not comply with the password security concept: At least one special character, at least one number, minimum length of 8 characters, one lower case and one upper case letter."
        };

        private static readonly string[] UnconnectedCardWarning = { "The module is not yet connected to any subnet." };

        private static readonly string[] StatesTexts = { "Stopped", "Running", "It's a fault: 'Ñ'" };

        private static readonly long[] StatesValues = { 0, 1, 2 };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedTextLists");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""State"", ""dataType"": ""Int"" } ] }"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateUnifiedTextList_States_ReadsBackAsAsked()
        {
            var written = Write(States);

            var entries = (JsonArray)written["entries"]!;
            CollectionAssert.AreEqual(StatesTexts, entries.Select(entry => entry!["text"]!.GetValue<string>()).ToArray());
            CollectionAssert.AreEqual(StatesValues, entries.Select(entry => entry!["value"]!.GetValue<long>()).ToArray());
            Assert.AreEqual(written.ToJsonString(), ((JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTextLists(HmiPath)["textLists"]!).Single()!.ToJsonString());
        }

        /// <remarks>The texts of every list live in one library TIA Portal renumbers; a second list must not take the first one's texts.</remarks>
        [TestMethod]
        public void CreateUnifiedTextList_SecondList_LeavesTheFirstAsItWas()
        {
            var first = Write(States);

            Write(@"{ ""name"": ""Modes"", ""entries"": [ { ""value"": 0, ""text"": ""Manual"" }, { ""value"": 1, ""text"": ""Auto"" } ] }");

            var lists = (JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTextLists(HmiPath)["textLists"]!;
            Assert.AreEqual(2, lists.Count);
            Assert.AreEqual(first.ToJsonString(), lists.Single(list => list!["name"]!.GetValue<string>() == "States")!.ToJsonString());
        }

        [TestMethod]
        public void CreateUnifiedTextList_ExistingList_IsReplacedAndTheOldOneBackedUp()
        {
            Write(States);
            var backup = Path.Combine(_testDirectory, "replace");

            var written = AssemblyHooks.SharedPortal.CreateUnifiedTextList(HmiPath, UnifiedTextListReader.Parse(@"{ ""name"": ""States"", ""entries"": [ { ""value"": 5, ""text"": ""Off"" } ] }"), backup);

            Assert.AreEqual(@"[{""value"":5,""text"":""Off""}]", written["entries"]!.ToJsonString());
            StringAssert.Contains(File.ReadAllText(Path.Combine(backup, "before", "States.TextLibrary.hmi.yml")), "Running", System.StringComparison.Ordinal);
        }

        /// <remarks>The read-back is compared by value: the order TIA Portal exports in is not the description's.</remarks>
        [TestMethod]
        public void CreateUnifiedTextList_EntriesInDescendingOrder_AreWritten()
        {
            var written = Write(@"{ ""name"": ""Countdown"", ""entries"": [ { ""value"": 2, ""text"": ""Two"" }, { ""value"": 1, ""text"": ""One"" }, { ""value"": 0, ""text"": ""Zero"" } ] }");

            Assert.AreEqual(3, ((JsonArray)written["entries"]!).Count);
        }

        [TestMethod]
        public void CreateUnifiedTextList_ExistingListSpeltOtherwise_ThrowsInvalidParamsAndChangesNothing()
        {
            var original = Write(States);

            var failure = Assert.ThrowsException<PortalException>(() => Write(States.Replace(@"""States""", @"""STATES""")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual(original.ToJsonString(), ((JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTextLists(HmiPath)["textLists"]!).Single()!.ToJsonString());
        }

        /// <remarks>A new list TIA Portal does not keep as asked is deleted again.</remarks>
        [TestMethod]
        public void CreateUnifiedTextList_NewListTiaPortalEmpties_IsDeletedAndThrowsWriteFailed()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateUnifiedTextList(HmiPath, Ranged("Ranged"), Path.Combine(_testDirectory, "ranged")));

            Assert.AreEqual(PortalErrorCode.WriteFailed, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "deleted again", System.StringComparison.Ordinal);
            Assert.AreEqual(0, ((JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTextLists(HmiPath)["textLists"]!).Count);
        }

        /// <remarks>A list TIA Portal does not keep as asked, over an existing one, puts the existing one back.</remarks>
        [TestMethod]
        public void CreateUnifiedTextList_ReplacementTiaPortalEmpties_PutsTheOldListBack()
        {
            var original = Write(States);

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateUnifiedTextList(HmiPath, Ranged("States"), Path.Combine(_testDirectory, "ranged")));

            Assert.AreEqual(PortalErrorCode.WriteFailed, failure.Code, failure.Message);
            Assert.AreEqual(original.ToJsonString(), ((JsonArray)AssemblyHooks.SharedPortal.GetUnifiedTextLists(HmiPath)["textLists"]!).Single()!.ToJsonString());
        }

        [TestMethod]
        public void DrawUnifiedScreen_FieldAndTextOnAList_ReadBackAndDrawnAgainUnchanged()
        {
            Write(States);
            var first = Draw(Shown);

            var second = Draw(first.ToJsonString());

            Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
            StringAssert.Contains(first.ToJsonString(), @"""ResourceList"":""States""", System.StringComparison.Ordinal);
            StringAssert.Contains(first.ToJsonString(), @"{""property"":""Text"",""tag"":""State"",""list"":""States""}", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void DrawUnifiedScreen_FieldAndTextOnAList_CompileWithNothingButTheKnownMessages()
        {
            Write(States);
            Draw(Shown);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = Described(report, CompilationSeverity.Error);
            var warnings = Described(report, CompilationSeverity.Warning);
            CollectionAssert.AreEquivalent(EncryptedTransferPasswordErrors, errors, string.Join(" | ", errors));
            CollectionAssert.AreEquivalent(UnconnectedCardWarning, warnings, string.Join(" | ", warnings));
        }

        /// <remarks>Openness takes a list that does not exist without a word; only the compile would report it.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_ListThatDoesNotExist_ThrowsInvalidStateAndDrawsNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Draw(Shown));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "States", System.StringComparison.Ordinal);
            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, "Drawn"));
        }

        private static UnifiedTextList Ranged(string name)
        {
            return new UnifiedTextList(name, new[] { new UnifiedTextListEntry(10, 20, "Low") });
        }

        private static string[] Described(CompilationReport report, CompilationSeverity severity)
        {
            return report.Messages
                .Where(message => message.Severity == severity && message.Description.Trim().Length > 0 && !message.Description.StartsWith("Compiling ", System.StringComparison.Ordinal))
                .Select(message => message.Description.Trim())
                .ToArray();
        }

        private JsonObject Write(string list)
        {
            return AssemblyHooks.SharedPortal.CreateUnifiedTextList(HmiPath, UnifiedTextListReader.Parse(list), Path.Combine(_testDirectory, "lists", System.Guid.NewGuid().ToString("N")));
        }

        private JsonObject Draw(string description)
        {
            return AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(description), Path.Combine(_testDirectory, "backup"));
        }
    }
}
