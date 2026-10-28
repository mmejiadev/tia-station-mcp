using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedAlarms;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>WinCC Unified alarm classes, discrete alarms and analog alarms (HMI-COVERAGE row R1), on internal tags.</summary>
    /// <remarks>
    /// The alarms here are raised by internal tags: a test project has no integrated connection, which
    /// Openness cannot create. Alarms on PLC-linked tags were measured on a copy of a class project.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test51UnifiedAlarms
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Alarms = @"{
            ""classes"": [ { ""name"": ""Tolva"", ""priority"": 10, ""stateMachine"": ""raiseClearRequiresAcknowledgement"",
                ""states"": { ""raised"": { ""backColor"": ""255, 128, 0"", ""textColor"": ""black"", ""flashing"": true }, ""cleared"": { ""backColor"": ""green"" } } } ],
            ""discrete"": [
                { ""name"": ""Tolva_Plena"", ""tag"": ""Alarms"", ""bit"": 3, ""class"": ""Tolva"", ""priority"": 3, ""text"": ""Tolva plena & bloquejada"", ""info"": ""Buidar la tolva"", ""origin"": ""Tolva"", ""area"": ""Linia 1"" },
                { ""name"": ""Paro"", ""tag"": ""Run"", ""trigger"": ""fallingEdge"", ""class"": ""Warning"", ""text"": ""Motor aturat"" } ],
            ""analog"": [
                { ""name"": ""Pressio_Alta"", ""tag"": ""Pressure"", ""condition"": ""upperLimitOrEqual"", ""value"": 8.5, ""class"": ""Tolva"", ""text"": ""Pressio alta"" },
                { ""name"": ""Nivell_Baix"", ""tag"": ""Level"", ""condition"": ""lowerLimit"", ""value"": -5, ""text"": ""Nivell baix"" } ] }";

        private const string StartScreen = @"{ ""name"": ""Start"", ""startScreen"": true, ""items"": [ { ""type"": ""alarmControl"", ""name"": ""Alarms_View"" } ] }";

        /// <summary>See <c>Test48UnifiedProcessDisplays</c>: what a Unified PC station compiles with once it has a start screen.</summary>
        private static readonly string[] EncryptedTransferPasswordErrors =
        {
            "The entered password confirmation does not match the password.",
            "The entered password does not comply with the password security concept: At least one special character, at least one number, minimum length of 8 characters, one lower case and one upper case letter."
        };

        private static readonly string[] UnconnectedCardWarning = { "The module is not yet connected to any subnet." };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedAlarms");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [
                { ""name"": ""Alarms"", ""dataType"": ""Word"" }, { ""name"": ""Run"", ""dataType"": ""Bool"" },
                { ""name"": ""Pressure"", ""dataType"": ""Real"" }, { ""name"": ""Level"", ""dataType"": ""Int"" } ] }"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateUnifiedAlarms_ClassAndAlarms_ReadBackAsWritten()
        {
            var created = Create(Alarms);

            var plena = (JsonObject)created["discrete"]!.AsArray().Single(alarm => alarm!["name"]!.GetValue<string>() == "Tolva_Plena")!;
            Assert.AreEqual("Alarms", plena["tag"]!.GetValue<string>());
            Assert.AreEqual(3u, plena["bit"]!.GetValue<uint>());
            Assert.AreEqual("Tolva", plena["class"]!.GetValue<string>());
            Assert.AreEqual("Tolva plena & bloquejada", plena["text"]!.GetValue<string>());
            Assert.AreEqual("Linia 1", plena["area"]!.GetValue<string>());
            Assert.AreEqual("fallingEdge", Alarm("discrete", "Paro")["trigger"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateUnifiedAlarms_Class_ReadsBackWithItsStates()
        {
            Create(Alarms);

            var tolva = (JsonObject)Read()["classes"]!.AsArray().Single(found => found!["name"]!.GetValue<string>() == "Tolva")!;
            Assert.IsFalse(tolva["system"]!.GetValue<bool>());
            Assert.AreEqual(10, tolva["priority"]!.GetValue<int>());
            Assert.AreEqual("raiseClearRequiresAcknowledgement", tolva["stateMachine"]!.GetValue<string>());
            Assert.AreEqual(@"{""backColor"":""255, 128, 0"",""textColor"":""0, 0, 0"",""flashing"":true}", tolva["states"]!["raised"]!.ToJsonString());
            Assert.AreEqual("0, 255, 0", tolva["states"]!["cleared"]!["backColor"]!.GetValue<string>());
        }

        [TestMethod]
        public void CreateUnifiedAlarms_AnalogAlarms_ReadBackWithTheirConditions()
        {
            Create(Alarms);

            Assert.AreEqual(8.5, Alarm("analog", "Pressio_Alta")["value"]!.GetValue<double>());
            Assert.AreEqual("upperLimitOrEqual", Alarm("analog", "Pressio_Alta")["condition"]!.GetValue<string>());
            Assert.AreEqual("Alarm", Alarm("analog", "Nivell_Baix")["class"]!.GetValue<string>());
        }

        [TestMethod]
        public void GetUnifiedAlarms_NewHmi_ListsTheSystemClasses()
        {
            var classes = Read()["classes"]!.AsArray();

            Assert.IsTrue(classes.Any(found => found!["name"]!.GetValue<string>() == "Warning" && found["system"]!.GetValue<bool>()), classes.ToJsonString());
        }

        [TestMethod]
        public void CreateUnifiedAlarms_Alarms_CompileWithNothingButTheKnownMessages()
        {
            Create(Alarms);
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(StartScreen), Path.Combine(_testDirectory, "backup"));

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = Described(report, CompilationSeverity.Error);
            var warnings = Described(report, CompilationSeverity.Warning);
            CollectionAssert.AreEquivalent(EncryptedTransferPasswordErrors, errors, string.Join(" | ", errors));
            CollectionAssert.AreEquivalent(UnconnectedCardWarning, warnings, string.Join(" | ", warnings));
        }

        /// <remarks>Each of these Openness takes without a word and the compile refuses (measured 2026-10-25).</remarks>
        [TestMethod]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""Level"", ""text"": ""x"" } ] }", "discrete alarm takes a Bool", DisplayName = "a discrete alarm on an Int")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""Alarms"", ""condition"": ""equal"", ""value"": 1, ""text"": ""x"" } ] }", "analog alarm takes a number", DisplayName = "an analog alarm on a Word")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""Alarms"", ""bit"": 16, ""text"": ""x"" } ] }", "bits 0 to 15", DisplayName = "a bit past a Word")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""No_Such_Tag"", ""text"": ""x"" } ] }", "does not exist", DisplayName = "a tag that does not exist")]
        [DataRow(@"{ ""discrete"": [ { ""name"": ""A"", ""tag"": ""Run"", ""class"": ""No_Such_Class"", ""text"": ""x"" } ] }", "No_Such_Class", DisplayName = "a class that does not exist")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""Level"", ""condition"": ""equal"", ""value"": 99999, ""text"": ""x"" } ] }", "outside the range", DisplayName = "a value outside the tag's type")]
        [DataRow(@"{ ""analog"": [ { ""name"": ""A"", ""tag"": ""Level"", ""condition"": ""equal"", ""value"": 1.5, ""text"": ""x"" } ] }", "whole number", DisplayName = "a fraction for an Int tag")]
        public void CreateUnifiedAlarms_WhatTheCompileRefuses_ThrowsInvalidStateAndCreatesNothing(string json, string named)
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(json));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, named, System.StringComparison.Ordinal);
            Assert.AreEqual(0, Read()["discrete"]!.AsArray().Count + Read()["analog"]!.AsArray().Count, failure.Message);
        }

        /// <remarks>The compile refuses it as "The tag/bit combination is also used for other HMI alarms".</remarks>
        [TestMethod]
        public void CreateUnifiedAlarms_BitThatRaisesAnotherAlarm_ThrowsInvalidStateAndCreatesNothing()
        {
            Create(@"{ ""discrete"": [ { ""name"": ""First"", ""tag"": ""Alarms"", ""bit"": 2, ""text"": ""x"" } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""discrete"": [ { ""name"": ""Second"", ""tag"": ""ALARMS"", ""bit"": 2, ""text"": ""y"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "First", System.StringComparison.Ordinal);
            Assert.AreEqual(1, Read()["discrete"]!.AsArray().Count);
        }

        [TestMethod]
        public void CreateUnifiedAlarms_SystemClassName_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""classes"": [ { ""name"": ""warning"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
        }

        /// <remarks>A class made in the call goes again when an alarm after it fails, the alarms before it too.</remarks>
        [TestMethod]
        public void CreateUnifiedAlarms_TiaPortalRefusesOne_RemovesWhatWasMadeBefore()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Create(@"{ ""classes"": [ { ""name"": ""Tolva"" } ],
                ""discrete"": [ { ""name"": ""Good"", ""tag"": ""Run"", ""class"": ""Tolva"", ""text"": ""x"" }, { ""name"": """ + new string('B', 300) + @""", ""tag"": ""Alarms"", ""text"": ""y"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "TIA Portal refused discrete alarm 'BBB", System.StringComparison.Ordinal);
            StringAssert.Contains(failure.Message, "removed again", System.StringComparison.Ordinal);
            Assert.AreEqual(0, Read()["discrete"]!.AsArray().Count, failure.Message);
            Assert.IsFalse(Read()["classes"]!.AsArray().Any(found => found!["name"]!.GetValue<string>() == "Tolva"), failure.Message);
        }

        private static string[] Described(CompilationReport report, CompilationSeverity severity)
        {
            return report.Messages
                .Where(message => message.Severity == severity && message.Description.Trim().Length > 0 && !message.Description.StartsWith("Compiling ", System.StringComparison.Ordinal))
                .Select(message => message.Description.Trim())
                .ToArray();
        }

        private static JsonObject Create(string json)
        {
            return AssemblyHooks.SharedPortal.CreateUnifiedAlarms(HmiPath, UnifiedAlarmListReader.Parse(json));
        }

        private static JsonObject Read()
        {
            return AssemblyHooks.SharedPortal.GetUnifiedAlarms(HmiPath);
        }

        private static JsonObject Alarm(string kind, string name)
        {
            return (JsonObject)Read()[kind]!.AsArray().Single(alarm => alarm!["name"]!.GetValue<string>() == name)!;
        }
    }
}
