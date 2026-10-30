using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedLogging;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>WinCC Unified data logs, alarm logs and logging tags (HMI-COVERAGE row R3), on internal tags.</summary>
    /// <remarks>What each refusal stands for was measured on 2026-10-28 by probes on a copy of UnifiedProject.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test53UnifiedLogging
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Logging = @"{
            ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
            ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Alarm"", ""Warning"" ] } ],
            ""loggingTags"": [
                { ""tag"": ""Level"", ""name"": ""Level_log"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T1s"" },
                { ""tag"": ""Pressure"", ""name"": ""Pressure_log"", ""dataLog"": ""Log_Tolva"" } ] }";

        private const string StartScreen = @"{ ""name"": ""Start"", ""startScreen"": true, ""items"": [ { ""type"": ""rectangle"", ""name"": ""Frame"" } ] }";

        /// <summary>See <c>Test48UnifiedProcessDisplays</c>: what a Unified PC station compiles with once it has a start screen.</summary>
        private static readonly string[] EncryptedTransferPasswordErrors =
        {
            "The entered password confirmation does not match the password.",
            "The entered password does not comply with the password security concept: At least one special character, at least one number, minimum length of 8 characters, one lower case and one upper case letter."
        };

        private static readonly string[] LoggedClasses = { "Alarm", "Warning" };

        private static readonly string[] OnlyWarning = { "Warning" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedLogging");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [
                { ""name"": ""Level"", ""dataType"": ""Int"" }, { ""name"": ""Pressure"", ""dataType"": ""Real"" } ] }"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateUnifiedLogging_LogsAndLoggingTags_ReadBackAsWritten()
        {
            var created = Create(Logging);

            Assert.AreEqual("Log_Tolva", created["dataLogs"]!.AsArray().Single()!["name"]!.GetValue<string>());
            var classes = created["alarmLogs"]!.AsArray().Single()!["classes"]!.AsArray().Select(found => found!.GetValue<string>()).ToArray();
            CollectionAssert.AreEquivalent(LoggedClasses, classes);
            var level = (JsonObject)created["loggingTags"]!.AsArray().Single(entry => entry!["name"]!.GetValue<string>() == "Level_log")!;
            Assert.AreEqual("cyclic", level["mode"]!.GetValue<string>());
            Assert.AreEqual("T1s", level["cycle"]!.GetValue<string>());
            Assert.AreEqual("onChange", ((JsonObject)created["loggingTags"]!.AsArray().Single(entry => entry!["name"]!.GetValue<string>() == "Pressure_log")!)["mode"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateUnifiedLogging_Logging_CompilesWithNothingButTheKnownMessages()
        {
            Create(Logging);
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(StartScreen), Path.Combine(_testDirectory, "backup"));

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = report.Messages
                .Where(message => message.Severity == CompilationSeverity.Error && message.Description.Trim().Length > 0 && !message.Description.StartsWith("Compiling ", System.StringComparison.Ordinal))
                .Select(message => message.Description.Trim())
                .ToArray();
            CollectionAssert.AreEquivalent(EncryptedTransferPasswordErrors, errors, string.Join(" | ", errors));
        }

        /// <remarks>A name that does not exist is a bad request; a name taken is the HMI's state.</remarks>
        [TestMethod]
        [DataRow(@"{ ""loggingTags"": [ { ""tag"": ""No_Such_Tag"", ""name"": ""L"", ""dataLog"": ""Log_Tolva"" } ], ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ] }", "does not exist", PortalErrorCode.InvalidParams, DisplayName = "a tag that does not exist")]
        [DataRow(@"{ ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""L"", ""dataLog"": ""No_Such_Log"" } ] }", "No_Such_Log", PortalErrorCode.InvalidParams, DisplayName = "a data log that does not exist")]
        [DataRow(@"{ ""alarmLogs"": [ { ""name"": ""A"", ""classes"": [ ""No_Such_Class"" ] } ] }", "No_Such_Class", PortalErrorCode.InvalidParams, DisplayName = "a class that does not exist")]
        public void CreateUnifiedLogging_WhatCannotBeCreated_IsRefusedAndCreatesNothing(string json, string named, PortalErrorCode code)
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(json));

            Assert.AreEqual(code, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, named, System.StringComparison.Ordinal);
            AssertNothingCreated(failure.Message);
        }

        /// <remarks>A class is changed only from empty, so moving one from another log is refused.</remarks>
        [TestMethod]
        public void CreateUnifiedLogging_AClassAlreadyLogged_IsNotMoved()
        {
            Create(@"{ ""alarmLogs"": [ { ""name"": ""First"", ""classes"": [ ""Alarm"" ] } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""alarmLogs"": [ { ""name"": ""Second"", ""classes"": [ ""alarm"" ] } ] }"));

            StringAssert.Contains(failure.Message, "already logged to 'First'", System.StringComparison.Ordinal);
            Assert.AreEqual("First", Read()["alarmLogs"]!.AsArray().Single()!["name"]!.GetValue<string>());
        }

        /// <remarks>
        /// T100ms on an internal tag is refused by Openness itself ("LoggingCycleConsistency check failed"),
        /// after the logs before it were made: they go again, and the class is emptied before its log goes.
        /// </remarks>
        [TestMethod]
        public void CreateUnifiedLogging_TiaPortalRefusesACycle_RemovesWhatWasMadeBefore()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{
                ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Warning"" ] } ],
                ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""Fast"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T100ms"" } ] }"));

            StringAssert.Contains(failure.Message, "TIA Portal refused logging tag 'Fast'", System.StringComparison.Ordinal);
            StringAssert.Contains(failure.Message, "removed again", System.StringComparison.Ordinal);
            AssertNothingCreated(failure.Message);
        }

        /// <remarks>
        /// A class left naming the deleted Alarmes would be refused as naming a deleted log; logging it to
        /// another log is what shows the undo emptied it.
        /// </remarks>
        [TestMethod]
        public void CreateUnifiedLogging_TiaPortalRefusesACycle_EmptiesTheClassAgain()
        {
            Assert.ThrowsException<PortalException>(() => Create(@"{
                ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Warning"" ] } ],
                ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""Fast"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T100ms"" } ] }"));

            var created = Create(@"{ ""alarmLogs"": [ { ""name"": ""Other"", ""classes"": [ ""Warning"" ] } ] }");

            CollectionAssert.AreEqual(OnlyWarning, created["alarmLogs"]!.AsArray().Single()!["classes"]!.AsArray().Select(found => found!.GetValue<string>()).ToArray());
        }

        /// <remarks>Without the factor, a logging tag at T1s x5 would read back as logging every second.</remarks>
        [TestMethod]
        public void CreateUnifiedLogging_ACycleFactor_ReadsBackWithTheCycle()
        {
            var created = Create(@"{ ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""Slow"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T1s"", ""cycleFactor"": 5 } ] }");

            var slow = created["loggingTags"]!.AsArray().Single()!;
            Assert.AreEqual("T1s", slow["cycle"]!.GetValue<string>());
            Assert.AreEqual(5u, slow["cycleFactor"]!.GetValue<uint>());
        }

        /// <remarks>The one existing thing the tool changes is a class's log; what it was is saved before it changes.</remarks>
        [TestMethod]
        public void CreateUnifiedLogging_AnAlarmLog_SavesTheClassesPreviousLogFirst()
        {
            Create(@"{ ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Warning"" ] } ] }");

            var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(BackupDirectory, "alarm-class-logs.json")))!.AsArray().Single()!;
            Assert.AreEqual("Warning", saved["class"]!.GetValue<string>());
            Assert.AreEqual(string.Empty, saved["log"]!.GetValue<string>());
        }

        private string BackupDirectory => Path.Combine(_testDirectory, "logging-backup");

        private static void AssertNothingCreated(string message)
        {
            var now = Read();
            Assert.AreEqual(0, now["dataLogs"]!.AsArray().Count + now["alarmLogs"]!.AsArray().Count + now["loggingTags"]!.AsArray().Count, $"{message} | {now.ToJsonString()}");
        }

        private JsonObject Create(string json)
        {
            return AssemblyHooks.SharedPortal.CreateUnifiedLogging(HmiPath, UnifiedLoggingListReader.Parse(json), BackupDirectory);
        }

        private static JsonObject Read()
        {
            return AssemblyHooks.SharedPortal.GetUnifiedLogging(HmiPath);
        }
    }
}
