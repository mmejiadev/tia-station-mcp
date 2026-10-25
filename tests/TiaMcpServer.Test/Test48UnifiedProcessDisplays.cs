using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// What a WinCC Unified screen shows of the process with no new drawing code: movement, rotation, a
    /// button enabled by a bit, and a bar and a gauge with their limits (HMI-COVERAGE rows A3, A4, A6, A7).
    /// </summary>
    /// <remarks>
    /// Each is a tag link or a range animation on a property the Openness V20 types have (read by
    /// reflection on 2026-10-25: <c>Left</c>, <c>Top</c>, <c>RotationAngle</c>, <c>Enabled</c>,
    /// <c>ProcessValue</c>, and the scale parts <c>StraightScale</c>/<c>CurvedScale</c> with
    /// <c>BeginValue</c> and <c>EndValue</c>), so what these tests settle is that TIA Portal takes them,
    /// reads them back and compiles them. Values are set away from their defaults, so one that is
    /// silently dropped fails.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test48UnifiedProcessDisplays
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Displays = @"{ ""name"": ""Drawn"", ""startScreen"": true, ""items"": [
            { ""type"": ""rectangle"", ""name"": ""Box"", ""properties"": { ""Width"": 40, ""Height"": 40 }, ""tags"": { ""Left"": ""PosX"", ""Top"": ""PosY"" } },
            { ""type"": ""rectangle"", ""name"": ""Blade"", ""properties"": { ""Left"": 200, ""Top"": 100, ""Width"": 80, ""Height"": 10 }, ""tags"": { ""RotationAngle"": ""Angle"" } },
            { ""type"": ""button"", ""name"": ""Start"", ""properties"": { ""Left"": 300, ""Top"": 20, ""Text"": ""Start"" }, ""animations"": [
                { ""property"": ""Enabled"", ""tag"": ""Ready"", ""ranges"": [ { ""from"": 0, ""value"": false }, { ""from"": 1, ""value"": true } ] } ] },
            { ""type"": ""bar"", ""name"": ""LevelBar"", ""properties"": { ""Left"": 20, ""Top"": 200, ""StraightScale.BeginValue"": 10 }, ""tags"": { ""ProcessValue"": ""Level"", ""StraightScale.EndValue"": ""MaxLevel"" } },
            { ""type"": ""gauge"", ""name"": ""LevelGauge"", ""properties"": { ""Left"": 200, ""Top"": 200, ""CurvedScale.BeginValue"": 5, ""CurvedScale.EndValue"": 150 }, ""tags"": { ""ProcessValue"": ""Level"" } } ] }";

        /// <summary>What a Unified PC station compiles with once it has a start screen: the encrypted-transfer password, set by hand (see <c>Test43PcStations</c>).</summary>
        private static readonly string[] EncryptedTransferPasswordErrors =
        {
            "The entered password confirmation does not match the password.",
            "The entered password does not comply with the password security concept: At least one special character, at least one number, minimum length of 8 characters, one lower case and one upper case letter."
        };

        /// <summary>The station's network card, plugged by <c>AddPcStation</c> and on no subnet here (row H8): the one warning allowed.</summary>
        private static readonly string[] UnconnectedCardWarning = { "The module is not yet connected to any subnet." };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedProcessDisplays");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [
                { ""name"": ""PosX"", ""dataType"": ""Int"" }, { ""name"": ""PosY"", ""dataType"": ""Int"" }, { ""name"": ""Angle"", ""dataType"": ""Int"" },
                { ""name"": ""Ready"", ""dataType"": ""Bool"" }, { ""name"": ""Level"", ""dataType"": ""Real"" }, { ""name"": ""MaxLevel"", ""dataType"": ""Real"" } ] }"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>One concept, the screen reads back what was asked, checked item by item on one draw.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_Displays_ReadBackEveryLinkAndLimit()
        {
            var drawn = Draw(Displays);

            Assert.AreEqual(@"{""Left"":""PosX"",""Top"":""PosY""}", Item(drawn, "Box")["tags"]!.ToJsonString());
            Assert.AreEqual(@"{""RotationAngle"":""Angle""}", Item(drawn, "Blade")["tags"]!.ToJsonString());
            Assert.AreEqual(@"[{""from"":0,""value"":false},{""from"":1,""value"":true}]", ((JsonArray)Item(drawn, "Start")["animations"]!).Single()!["ranges"]!.ToJsonString());
            Assert.AreEqual(@"{""ProcessValue"":""Level"",""StraightScale.EndValue"":""MaxLevel""}", Item(drawn, "LevelBar")["tags"]!.ToJsonString());
            Assert.AreEqual(10.0, Item(drawn, "LevelBar")["properties"]!["StraightScale.BeginValue"]!.GetValue<double>());
            Assert.AreEqual(@"{""ProcessValue"":""Level""}", Item(drawn, "LevelGauge")["tags"]!.ToJsonString());
            Assert.AreEqual(5.0, Item(drawn, "LevelGauge")["properties"]!["CurvedScale.BeginValue"]!.GetValue<double>());
            Assert.AreEqual(150.0, Item(drawn, "LevelGauge")["properties"]!["CurvedScale.EndValue"]!.GetValue<double>());
        }

        [TestMethod]
        public void DrawUnifiedScreen_DisplaysReadBackAndDrawnAgain_AreUnchanged()
        {
            var first = Draw(Displays);

            var second = Draw(first.ToJsonString());

            Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
        }

        /// <remarks>
        /// Every error in the project counts, not only the screen's, and so does every warning: a link of
        /// the wrong type would be reported against the tag, not the screen.
        /// </remarks>
        [TestMethod]
        public void DrawUnifiedScreen_Displays_CompileWithNothingButTheKnownPasswordErrors()
        {
            Draw(Displays);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = Described(report, CompilationSeverity.Error);
            var warnings = Described(report, CompilationSeverity.Warning);
            CollectionAssert.AreEquivalent(EncryptedTransferPasswordErrors, errors, string.Join(" | ", errors));
            CollectionAssert.AreEquivalent(UnconnectedCardWarning, warnings, string.Join(" | ", warnings));
        }

        /// <summary>The messages of one severity that say something, without the "Compiling finished" summary.</summary>
        private static string[] Described(CompilationReport report, CompilationSeverity severity)
        {
            return report.Messages
                .Where(message => message.Severity == severity && message.Description.Trim().Length > 0 && !message.Description.StartsWith("Compiling ", System.StringComparison.Ordinal))
                .Select(message => message.Description.Trim())
                .ToArray();
        }

        private static JsonObject Item(JsonObject screen, string name)
        {
            return (JsonObject)((JsonArray)screen["items"]!).Single(item => item!["name"]!.GetValue<string>() == name)!;
        }

        private JsonObject Draw(string description)
        {
            return AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(description), Path.Combine(_testDirectory, "backup"));
        }
    }
}
