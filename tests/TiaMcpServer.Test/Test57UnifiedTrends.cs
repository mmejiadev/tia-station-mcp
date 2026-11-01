using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedLogging;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;
using TiaMcpServer.UnifiedTrends;

namespace TiaMcpServer.Test
{
    /// <summary>A WinCC Unified trend control filled with a logged and a live trend (HMI-COVERAGE U-C, C14).</summary>
    /// <remarks>What each value and refusal stands for was measured on 2026-10-30 by probes on a new project.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test57UnifiedTrends
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Trends = @"{ ""screen"": ""Start"", ""control"": ""Trend_1"",
            ""trends"": [ { ""tag"": ""Level"", ""loggingTag"": ""Level_log"", ""name"": ""Nivell"", ""color"": ""#FF0000"", ""mode"": ""stepped"" }, { ""tag"": ""Pressure"" } ],
            ""timeRange"": { ""factor"": 10, ""base"": ""minute"" } }";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedTrends");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [
                { ""name"": ""Level"", ""dataType"": ""Int"" }, { ""name"": ""Pressure"", ""dataType"": ""Real"" } ] }"));
            AssemblyHooks.SharedPortal.CreateUnifiedLogging(HmiPath, UnifiedLoggingListReader.Parse(@"{
                ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""Level_log"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T1s"" } ] }"), Path.Combine(_testDirectory, "logging"));
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Start"", ""startScreen"": true,
                ""items"": [ { ""type"": ""trendControl"", ""name"": ""Trend_1"", ""properties"": { ""Left"": 0, ""Top"": 0, ""Width"": 600, ""Height"": 400 } } ] }"), Path.Combine(_testDirectory, "screen"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void SetUnifiedTrends_ALoggedAndALiveTrend_ReadBackWithTheirSourcesAndRange()
        {
            var area = (JsonObject)Set(Trends)["areas"]!.AsArray()[0]!;

            var trends = area["trends"]!.AsArray();
            Assert.AreEqual("Level:Level_log", trends[0]!["source"]!.GetValue<string>());
            Assert.AreEqual("Nivell", trends[0]!["name"]!.GetValue<string>());
            Assert.AreEqual("#FF0000", trends[0]!["color"]!.GetValue<string>());
            Assert.AreEqual("Stepped", trends[0]!["mode"]!.GetValue<string>());
            Assert.AreEqual("Pressure", trends[1]!["source"]!.GetValue<string>());
            Assert.AreEqual(10, area["timeRange"]!["factor"]!.GetValue<int>());
            Assert.AreEqual("Minute", area["timeRange"]!["base"]!.GetValue<string>());
        }

        [TestMethod]
        public void SetUnifiedTrends_TwoTrends_CompileWithNoErrorOfTheControl()
        {
            Set(Trends);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var controlErrors = report.Messages
                .Where(message => message.Severity == CompilationSeverity.Error && (message.Path + message.Description).Contains("Trend_1"))
                .Select(message => message.ToString())
                .ToArray();
            Assert.AreEqual(0, controlErrors.Length, string.Join(" | ", controlErrors));
        }

        [TestMethod]
        public void SetUnifiedTrends_AControlAlreadyFilled_IsRefusedAndKeepsItsTrends()
        {
            Set(Trends);

            var failure = Assert.ThrowsException<PortalException>(() => Set(Trends));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            Assert.AreEqual(2, Read()["areas"]![0]!["trends"]!.AsArray().Count);
        }

        [TestMethod]
        public void SetUnifiedTrends_ALoggingTagThatDoesNotExist_IsRefusedAndLeavesTheControlEmpty()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Set(Trends.Replace("Level_log", "No_Such_log")));

            StringAssert.Contains(failure.Message, "it has: Level_log", System.StringComparison.Ordinal);
            Assert.AreEqual(string.Empty, Read()["areas"]![0]!["trends"]![0]!["source"]!.GetValue<string>());
        }

        private JsonObject Set(string json)
        {
            return AssemblyHooks.SharedPortal.SetUnifiedTrends(HmiPath, UnifiedTrendListReader.Parse(json), Path.Combine(_testDirectory, "backup"));
        }

        private static JsonObject Read()
        {
            return (JsonObject)AssemblyHooks.SharedPortal.GetUnifiedTrends(HmiPath)["trendControls"]!.AsArray().Single()!;
        }
    }
}
