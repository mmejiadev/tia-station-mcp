using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Drawing WinCC Unified screens through the object model, every item type and every property.
    /// </summary>
    /// <remarks>
    /// The HMI is a PC station with WinCC Unified PC RT, built as <c>Test43PcStations</c> measured it.
    /// The round trip is the coverage test the HMI checklist asks for (row U-P): every writable property
    /// of every item type is read, written back and read again, so a property the writer cannot set
    /// fails here by name.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test44UnifiedScreens
    {
        private const string StationName = "PC-System_1";
        private const string HmiPath = "PC-System_1/HMI_RT_1";
        private const string ScreenName = "Drawn";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedScreens");
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPc);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void DrawUnifiedScreen_EachItemTypeOnItsOwn_IsDrawn()
        {
            var refused = UnifiedItemTypes.All
                .Select(type => (Type: type, Failure: TryDraw(EveryItemType(type))))
                .Where(attempt => attempt.Failure != null)
                .Select(attempt => $"{UnifiedItemTypes.ShortName(attempt.Type)}: {attempt.Failure}")
                .ToList();

            Assert.AreEqual(0, refused.Count, string.Join(" | ", refused));
        }

        [TestMethod]
        public void DrawUnifiedScreen_EveryItemType_DrawsEachOne()
        {
            var drawn = Draw(EveryItemType());

            var types = ((JsonArray)drawn["items"]!).Select(item => item!["type"]!.GetValue<string>()).ToArray();
            CollectionAssert.AreEquivalent(UnifiedItemTypes.All.Select(UnifiedItemTypes.ShortName).ToArray(), types);
        }

        /// <remarks>
        /// Lists every property TIA Portal refuses when an item read back is drawn again, all types in one
        /// run: a refused property is noted, taken out, and the item drawn again, so one refusal does not
        /// hide the next.
        /// </remarks>
        [TestMethod]
        public void DrawUnifiedScreen_EachItemTypeReadBack_IsDrawnAgain()
        {
            var refused = UnifiedItemTypes.All.SelectMany(RefusedOnRedraw).ToList();

            Assert.AreEqual(0, refused.Count, string.Join(" | ", refused));
        }

        [TestMethod]
        public void DrawUnifiedScreen_ScreenReadBackAndDrawnAgain_IsUnchanged()
        {
            var first = Draw(EveryItemType());

            var second = Draw(first.ToJsonString());

            Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
        }

        [TestMethod]
        public void DrawUnifiedScreen_Values_ReadBackAsWritten()
        {
            const string Screen = @"{ ""name"": ""Drawn"", ""properties"": { ""BackColor"": ""10, 20, 30"" }, ""items"": [
                { ""type"": ""rectangle"", ""name"": ""Tank"", ""properties"": { ""Left"": 40, ""Top"": 60, ""Width"": 120, ""BackColor"": ""#0000FF"", ""Corners.TopLeftRadius"": 8 } },
                { ""type"": ""text"", ""name"": ""Title"", ""properties"": { ""Text"": ""Tank 1"", ""Font.Size"": 14 } },
                { ""type"": ""polyline"", ""name"": ""Pipe"", ""points"": [[0, 0], [60, 0], [60, 90]] } ] }";

            var drawn = Draw(Screen);

            Assert.AreEqual("10, 20, 30", drawn["properties"]!["BackColor"]!.GetValue<string>());
            Assert.AreEqual("0, 0, 255", Item(drawn, "Tank")["properties"]!["BackColor"]!.GetValue<string>());
            Assert.AreEqual(8u, Item(drawn, "Tank")["properties"]!["Corners.TopLeftRadius"]!.GetValue<uint>());
            Assert.AreEqual("Tank 1", Item(drawn, "Title")["properties"]!["Text"]!.GetValue<string>());
            Assert.AreEqual("[[0,0],[60,0],[60,90]]", Item(drawn, "Pipe")["points"]!.ToJsonString());
        }

        [TestMethod]
        public void DrawUnifiedScreen_StartScreen_BecomesTheRuntimesStartScreen()
        {
            var drawn = Draw(@"{ ""name"": ""Drawn"", ""startScreen"": true, ""items"": [] }");

            Assert.IsTrue(drawn["startScreen"]!.GetValue<bool>());
        }

        [TestMethod]
        public void DrawUnifiedScreen_ExistingScreen_IsBackedUpAndItsItemsReplaced()
        {
            Draw(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""circle"", ""name"": ""Old"" } ] }");
            var backup = Path.Combine(_testDirectory, "second");

            var drawn = AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""rectangle"", ""name"": ""New"" } ] }"), backup);

            Assert.AreEqual("New", ((JsonArray)drawn["items"]!).Single()!["name"]!.GetValue<string>());
            StringAssert.Contains(File.ReadAllText(Directory.GetFiles(backup, "*.json").Single()), "\"Old\"");
        }

        [TestMethod]
        public void DrawUnifiedScreen_UnknownProperty_ThrowsInvalidParamsAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""rectangle"", ""properties"": { ""Colour"": ""red"" } } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, ScreenName));
        }

        [TestMethod]
        public void DrawUnifiedScreen_LinkToMissingTag_ThrowsInvalidStateAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""ioField"", ""tags"": { ""ProcessValue"": ""NoSuchTag"" } } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, ScreenName));
        }

        [TestMethod]
        public void DrawUnifiedScreen_EveryItemType_Compiles()
        {
            Draw(EveryItemType());

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = report.Messages.Where(message => message.Severity == CompilationSeverity.Error && message.Path.Contains(ScreenName)).ToList();
            Assert.AreEqual(0, errors.Count, string.Join(" | ", errors.Select(message => $"{message.Path}: {message.Description}")));
        }

        [TestMethod]
        public void DescribeUnifiedItemType_EveryType_ExplainsEveryPropertyItCannotSet()
        {
            foreach (var type in UnifiedItemTypes.All)
            {
                var properties = (JsonArray)Portal.DescribeUnifiedItemType(UnifiedItemTypes.ShortName(type))["properties"]!;

                var unexplained = properties.Where(property => !property!["settable"]!.GetValue<bool>() && property["why"] == null).ToList();
                Assert.AreEqual(0, unexplained.Count, $"{type}: {string.Join(", ", unexplained.Select(property => property!["path"]))}");
            }
        }

        private JsonObject Draw(string description)
        {
            return AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(description), Path.Combine(_testDirectory, "backup"));
        }

        private List<string> RefusedOnRedraw(string type)
        {
            const int MaxRefusals = 20;
            var item = ((JsonArray)Draw(EveryItemType(type))["items"]!)[0]!.ToJsonString();
            var refused = new List<string>();

            for (var attempt = 0; attempt < MaxRefusals; attempt++)
            {
                var failure = TryDraw($@"{{ ""name"": ""{ScreenName}"", ""items"": [ {item} ] }}");
                var setter = failure == null ? null : Regex.Match(failure, @"set_(\w+)' of type '[\w.]*\.(\w+)'[\s\S]*?\n\s*\n([^\r\n]*)");
                if (setter == null)
                {
                    return refused;
                }

                if (!setter.Success)
                {
                    refused.Add($"{UnifiedItemTypes.ShortName(type)}: {failure}");
                    return refused;
                }

                refused.Add($"{setter.Groups[2].Value}.{setter.Groups[1].Value} ({setter.Groups[3].Value.Trim()})");
                item = WithoutProperty(item, setter.Groups[1].Value);
            }

            return refused;
        }

        private static string WithoutProperty(string item, string name)
        {
            var parsed = (JsonObject)JsonNode.Parse(item)!;
            var properties = (JsonObject)parsed["properties"]!;

            foreach (var path in properties.Select(property => property.Key).Where(path => path == name || path.EndsWith("." + name, System.StringComparison.Ordinal)).ToList())
            {
                properties.Remove(path);
            }

            return parsed.ToJsonString();
        }

        private string? TryDraw(string description)
        {
            try
            {
                Draw(description);
                return null;
            }
            catch (PortalException failure)
            {
                return failure.InnerException?.Message.Trim() ?? failure.Message;
            }
        }

        private static JsonObject Item(JsonObject screen, string name)
        {
            return (JsonObject)((JsonArray)screen["items"]!).Single(item => item!["name"]!.GetValue<string>() == name)!;
        }

        /// <summary>One item of every drawable type, placed apart so none hides another.</summary>
        /// <remarks>
        /// Circles, ellipses and their arcs and segments have no <c>Left</c> and <c>Top</c>: they are placed
        /// by their centre (measured 2026-10-21), so each item is placed by what its type has.
        /// </remarks>
        private static string EveryItemType(params string[] only)
        {
            const int Spacing = 30;

            var items = (only.Length > 0 ? only : UnifiedItemTypes.All).Select((type, index) =>
            {
                var (horizontal, vertical) = HasProperty(type, "Left") ? ("Left", "Top") : ("CenterX", "CenterY");
                var item = new JsonObject
                {
                    ["type"] = UnifiedItemTypes.ShortName(type),
                    ["properties"] = new JsonObject { [horizontal] = (index % 6) * 4 * Spacing, [vertical] = (index / 6) * 3 * Spacing }
                };

                if (UnifiedItemTypes.IsPointBased(type))
                {
                    item["points"] = JsonNode.Parse("[[0, 0], [40, 0], [40, 40]]");
                }

                return (JsonNode)item;
            });

            return new JsonObject { ["name"] = ScreenName, ["items"] = new JsonArray(items.ToArray()) }.ToJsonString();
        }

        private static bool HasProperty(string type, string path)
        {
            var properties = (JsonArray)Portal.DescribeUnifiedItemType(UnifiedItemTypes.ShortName(type))["properties"]!;

            return properties.Any(property => property!["path"]!.GetValue<string>() == path);
        }
    }
}
