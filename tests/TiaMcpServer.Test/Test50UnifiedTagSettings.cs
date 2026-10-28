using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// WinCC Unified tag settings, arrays, tag table groups and system tags (HMI-COVERAGE rows T4, T5,
    /// T7, T8 and T9), on internal tags.
    /// </summary>
    /// <remarks>
    /// A linked tag's settings (acquisition, scaling, substitute value) need an integrated connection,
    /// which Openness V20 cannot create, so a project built here has none; they were measured on a copy of
    /// a class project that has one, outside the repository. Their refusals on an internal tag are tested
    /// without TIA Portal, in <c>UnifiedTagSettingsReaderTests</c>.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test50UnifiedTagSettings
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Settings = @"{ ""tags"": [
            { ""name"": ""Level_Max"", ""dataType"": ""Int"", ""initialValue"": 90 },
            { ""name"": ""Level"", ""dataType"": ""Int"", ""comment"": ""Nivell del dipòsit"", ""initialValue"": 7, ""persistent"": true, ""limits"": { ""lower"": -5, ""upper"": ""Level_Max"" } },
            { ""name"": ""Ratio"", ""dataType"": ""Real"", ""initialValue"": 1.5, ""limits"": { ""lower"": -1.5, ""upper"": 1e3 } },
            { ""name"": ""Ready"", ""dataType"": ""Bool"", ""initialValue"": true },
            { ""name"": ""Label"", ""dataType"": ""WString"", ""initialValue"": ""Tolva"", ""maxLength"": 20 },
            { ""name"": ""Levels"", ""dataType"": ""Array[0..3] of Real"" } ] }";

        private const string StartScreen = @"{ ""name"": ""Start"", ""startScreen"": true, ""items"": [
            { ""type"": ""ioField"", ""name"": ""LevelField"", ""tags"": { ""ProcessValue"": ""Level"" } } ] }";

        /// <summary>See <c>Test48UnifiedProcessDisplays</c>: what a Unified PC station compiles with once it has a start screen.</summary>
        private static readonly string[] EncryptedTransferPasswordErrors =
        {
            "The entered password confirmation does not match the password.",
            "The entered password does not comply with the password security concept: At least one special character, at least one number, minimum length of 8 characters, one lower case and one upper case letter."
        };

        private static readonly string[] UnconnectedCardWarning = { "The module is not yet connected to any subnet." };

        private static readonly string[] NestedGroups = { "Plant", "Plant/Area_1" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedTagSettings");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateUnifiedTags_InternalTagWithSettings_ReadsBackAsWritten()
        {
            Create(Settings);

            var level = Tag("Level");
            Assert.AreEqual("Nivell del dipòsit", level["comment"]!.GetValue<string>());
            Assert.AreEqual(7m, level["initialValue"]!.GetValue<decimal>());
            Assert.IsTrue(level["persistent"]!.GetValue<bool>());
            Assert.AreEqual(@"{""lower"":-5,""upper"":""Level_Max""}", level["limits"]!.ToJsonString());
        }

        [TestMethod]
        public void CreateUnifiedTags_RealBoolAndText_ReadBackAsTiaPortalStoresThem()
        {
            Create(Settings);

            Assert.AreEqual(@"{""lower"":-1.5,""upper"":1000}", Tag("Ratio")["limits"]!.ToJsonString());
            Assert.AreEqual(1m, Tag("Ready")["initialValue"]!.GetValue<decimal>());
            Assert.AreEqual("Tolva", Tag("Label")["initialValue"]!.GetValue<string>());
            Assert.AreEqual(20u, Tag("Label")["maxLength"]!.GetValue<uint>());
        }

        [TestMethod]
        public void CreateUnifiedTags_Array_ReadsBackAsAnArray()
        {
            Create(Settings);

            var levels = Tag("Levels");
            Assert.AreEqual("Array [0..3] of Real", levels["dataType"]!.GetValue<string>());
            Assert.AreEqual("array", levels["tagType"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateUnifiedTags_InternalTagsWithSettings_CompileWithNothingButTheKnownMessages()
        {
            Create(Settings);
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(StartScreen), Path.Combine(_testDirectory, "backup"));

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = Described(report, CompilationSeverity.Error);
            var warnings = Described(report, CompilationSeverity.Warning);
            CollectionAssert.AreEquivalent(EncryptedTransferPasswordErrors, errors, string.Join(" | ", errors));
            CollectionAssert.AreEquivalent(UnconnectedCardWarning, warnings, string.Join(" | ", warnings));
        }

        [TestMethod]
        public void CreateUnifiedTags_TableInNestedGroups_CreatesTheGroupsAndLandsInIt()
        {
            var created = Create(@"{ ""newTables"": [ ""Plant/Area_1/Valves"" ], ""tags"": [ { ""name"": ""V1"", ""table"": ""Valves"", ""dataType"": ""Bool"" } ] }");

            Assert.AreEqual("Plant/Area_1/Valves", created.Single()!["table"]!.GetValue<string>());
            CollectionAssert.AreEqual(NestedGroups, Read()["tableGroups"]!.AsArray().Select(group => group!.GetValue<string>()).ToArray());
        }

        /// <remarks>A second path through an existing group adds to it rather than making another of the same name.</remarks>
        [TestMethod]
        public void CreateUnifiedTags_TableInAnExistingGroup_ReusesTheGroup()
        {
            Create(@"{ ""newTables"": [ ""Plant/Motors"" ], ""tags"": [ { ""name"": ""M1"", ""table"": ""Motors"", ""dataType"": ""Bool"" } ] }");

            Create(@"{ ""newTables"": [ ""PLANT/Valves"" ], ""tags"": [ { ""name"": ""V1"", ""table"": ""Valves"", ""dataType"": ""Bool"" } ] }");

            Assert.AreEqual("Plant/Valves", Tag("V1")["table"]!.GetValue<string>());
            Assert.AreEqual(1, Read()["tableGroups"]!.AsArray().Count);
        }

        /// <remarks>A table is found by its name, unique on the HMI; a path written with it must be the table's own.</remarks>
        [TestMethod]
        public void CreateUnifiedTags_TablePathThatIsNotTheTables_ThrowsInvalidStateAndCreatesNothing()
        {
            Create(@"{ ""newTables"": [ ""Plant/Area_1/Valves"" ], ""tags"": [ { ""name"": ""V1"", ""table"": ""Plant/Area_1/Valves"", ""dataType"": ""Bool"" } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""tags"": [ { ""name"": ""V2"", ""table"": ""Wrong/Valves"", ""dataType"": ""Bool"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "Plant/Area_1/Valves", System.StringComparison.Ordinal);
            Assert.IsFalse(TagNames().Contains("V2"));
        }

        [TestMethod]
        public void CreateUnifiedTags_TableNameTakenInAnotherGroup_ThrowsInvalidStateAndCreatesNothing()
        {
            Create(@"{ ""newTables"": [ ""Plant/Valves"" ], ""tags"": [ { ""name"": ""V1"", ""table"": ""Valves"", ""dataType"": ""Bool"" } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""newTables"": [ ""Area/Valves"" ], ""tags"": [ { ""name"": ""V2"", ""dataType"": ""Bool"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.IsFalse(TagNames().Contains("V2"));
            Assert.IsFalse(Read()["tableGroups"]!.AsArray().Any(group => group!.GetValue<string>() == "Area"));
        }

        /// <remarks>The tags, tables and groups made before a refused tag are all removed again: the call stays all or nothing.</remarks>
        [TestMethod]
        public void CreateUnifiedTags_TiaPortalRefusesOneAfterNewGroups_RemovesTheGroupsToo()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""newTables"": [ ""Plant/Area_1/Valves"" ], ""tags"": [
                { ""name"": ""V1"", ""table"": ""Valves"", ""dataType"": ""Bool"" }, { ""name"": """ + new string('B', 129) + @""", ""dataType"": ""Bool"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "removed again", System.StringComparison.Ordinal);
            Assert.AreEqual(0, Read()["tableGroups"]!.AsArray().Count, Read().ToJsonString());
            Assert.IsFalse(TagNames().Contains("V1"));
        }

        [TestMethod]
        public void GetUnifiedTags_SystemTags_AreListedWithTheirTypes()
        {
            var systemTags = Read()["systemTags"]!.AsArray();

            Assert.IsTrue(systemTags.Any(tag => tag!["name"]!.GetValue<string>() == "@UserName" && tag["dataType"]!.GetValue<string>() == "WString"), systemTags.ToJsonString());
        }

        /// <remarks>Openness creates a tag named like a system tag without a word (measured 2026-10-25).</remarks>
        [TestMethod]
        public void CreateUnifiedTags_NameOfASystemTag_ThrowsInvalidStateAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""tags"": [ { ""name"": ""@UserName"", ""dataType"": ""WString"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.IsFalse(TagNames().Contains("@UserName"));
        }

        /// <remarks>Openness takes a limit tag that does not exist; only the compile reports it (measured 2026-10-25).</remarks>
        [TestMethod]
        public void CreateUnifiedTags_LimitFromATagThatDoesNotExist_ThrowsInvalidStateAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""tags"": [ { ""name"": ""Level"", ""dataType"": ""Int"", ""limits"": { ""upper"": ""No_Such_Tag"" } } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "No_Such_Tag", System.StringComparison.Ordinal);
            Assert.IsFalse(TagNames().Contains("Level"));
        }

        /// <remarks>Openness takes it; the compile refuses it with "Only HMI tags of the same data type are allowed" (measured 2026-10-25).</remarks>
        [TestMethod]
        public void CreateUnifiedTags_LimitFromATagOfAnotherType_ThrowsInvalidParamsAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""tags"": [
                { ""name"": ""Ceiling"", ""dataType"": ""Real"" }, { ""name"": ""Level"", ""dataType"": ""Int"", ""limits"": { ""upper"": ""Ceiling"" } } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "same data type", System.StringComparison.Ordinal);
            Assert.IsFalse(TagNames().Contains("Ceiling") || TagNames().Contains("Level"), failure.Message);
        }

        private static string[] Described(CompilationReport report, CompilationSeverity severity)
        {
            return report.Messages
                .Where(message => message.Severity == severity && message.Description.Trim().Length > 0 && !message.Description.StartsWith("Compiling ", System.StringComparison.Ordinal))
                .Select(message => message.Description.Trim())
                .ToArray();
        }

        private static JsonArray Create(string json)
        {
            return AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(json));
        }

        private static JsonObject Read()
        {
            return AssemblyHooks.SharedPortal.GetUnifiedTags(HmiPath);
        }

        private static JsonObject Tag(string name)
        {
            return (JsonObject)Read()["tags"]!.AsArray().Single(tag => tag!["name"]!.GetValue<string>() == name)!;
        }

        private static string[] TagNames()
        {
            return Read()["tags"]!.AsArray().Select(tag => tag!["name"]!.GetValue<string>()).ToArray();
        }
    }
}
