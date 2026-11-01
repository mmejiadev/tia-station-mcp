using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedLogging;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>WinCC Unified log settings: size, period, segments and backup (HMI-COVERAGE row R3).</summary>
    /// <remarks>What each refusal stands for was measured on 2026-10-30 by probes on a new project.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test56UnifiedLogSettings
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedLogSettings");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Level"", ""dataType"": ""Int"" } ] }"));
            AssemblyHooks.SharedPortal.CreateUnifiedLogging(HmiPath, UnifiedLoggingListReader.Parse(@"{
                ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Alarm"" ] } ] }"), Path.Combine(_testDirectory, "logging"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void SetUnifiedLogSettings_ShorterLogAndSegments_ReadBackAsWritten()
        {
            var log = Set(@"{ ""logs"": [ { ""name"": ""Log_Tolva"", ""maxSize"": 500, ""duration"": { ""days"": 2, ""hours"": 12 },
                ""segment"": { ""maxSize"": 50, ""duration"": { ""hours"": 6 } } } ] }");

            Assert.AreEqual(500u, log["maxSize"]!.GetValue<uint>());
            Assert.AreEqual(12u, log["duration"]!["hours"]!.GetValue<uint>());
            Assert.AreEqual(50u, log["segment"]!["maxSize"]!.GetValue<uint>());
            Assert.AreEqual(6u, log["segment"]!["duration"]!["hours"]!.GetValue<uint>());
        }

        /// <remarks>A segment longer than the log it is shrunk to would be refused write by write, without the plan's order.</remarks>
        [TestMethod]
        public void SetUnifiedLogSettings_ALogShrunkBelowItsOldSegment_TakesTheNewSegmentFirst()
        {
            var log = Set(@"{ ""logs"": [ { ""name"": ""Alarmes"", ""maxSize"": 80, ""duration"": { ""hours"": 12 },
                ""segment"": { ""maxSize"": 20, ""duration"": { ""hours"": 1 } } } ] }");

            Assert.AreEqual(80u, log["maxSize"]!.GetValue<uint>());
            Assert.AreEqual(0u, log["duration"]!["days"]!.GetValue<uint>());
        }

        [TestMethod]
        public void SetUnifiedLogSettings_ASegmentAboveItsLog_IsRefusedAndChangesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Set(@"{ ""logs"": [ { ""name"": ""Log_Tolva"", ""maxSize"": 50 } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual(1000u, Read("dataLogs", "Log_Tolva")["maxSize"]!.GetValue<uint>());
        }

        [TestMethod]
        public void SetUnifiedLogSettings_ALogThatDoesNotExist_IsRefusedNamingTheLogs()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Set(@"{ ""logs"": [ { ""name"": ""No_Such_Log"", ""maxSize"": 500 } ] }"));

            StringAssert.Contains(failure.Message, "Log_Tolva, Alarmes", System.StringComparison.Ordinal);
        }

        /// <remarks>An empty backup path is refused only by the compile ("The primary backup path of the log is empty"), so a path is what is checked here.</remarks>
        [TestMethod]
        public void SetUnifiedLogSettings_ABackupPath_CompilesWithNoLogError()
        {
            Set(@"{ ""logs"": [ { ""name"": ""Log_Tolva"", ""backupPath"": ""D:\\Backup"" } ] }");

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var logErrors = report.Messages
                .Where(message => message.Severity == CompilationSeverity.Error && (message.Path + message.Description).Contains("Log_Tolva"))
                .Select(message => message.ToString())
                .ToArray();
            Assert.AreEqual(0, logErrors.Length, string.Join(" | ", logErrors));
            Assert.AreEqual(@"D:\Backup", Read("dataLogs", "Log_Tolva")["backupPath"]!.GetValue<string>());
        }

        [TestMethod]
        public void GetUnifiedLogging_ANewLog_ReadsTheMeasuredDefaults()
        {
            var settings = Read("dataLogs", "Log_Tolva");

            Assert.AreEqual(1000u, settings["maxSize"]!.GetValue<uint>());
            Assert.AreEqual(7u, settings["duration"]!["days"]!.GetValue<uint>());
            Assert.AreEqual(100u, settings["segment"]!["maxSize"]!.GetValue<uint>());
            Assert.AreEqual(string.Empty, settings["backupPath"]!.GetValue<string>());
        }

        private JsonObject Set(string json)
        {
            var changed = AssemblyHooks.SharedPortal.SetUnifiedLogSettings(HmiPath, UnifiedLogSettingsListReader.Parse(json), Path.Combine(_testDirectory, "backup"));

            return (JsonObject)changed["logs"]!.AsArray().Single()!;
        }

        private static JsonObject Read(string kind, string name)
        {
            var logs = AssemblyHooks.SharedPortal.GetUnifiedLogging(HmiPath)[kind]!.AsArray();

            return (JsonObject)logs.Single(log => log!["name"]!.GetValue<string>() == name)!["settings"]!;
        }
    }
}
