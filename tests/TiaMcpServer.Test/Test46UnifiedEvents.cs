using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The JavaScript events of WinCC Unified screen items, drawn through <c>DrawUnifiedScreen</c>.
    /// </summary>
    /// <remarks>
    /// The station is a PC with WinCC Unified PC RT, as in <c>Test44UnifiedScreens</c>. The jog button
    /// is the class one: a bit set while the button is down and reset when it comes up.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test46UnifiedEvents
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string JogScreen = @"{ ""name"": ""Drawn"", ""items"": [
            { ""type"": ""button"", ""name"": ""Jog"", ""events"": [
                { ""event"": ""down"", ""function"": ""setBit"", ""tag"": ""Motor"" },
                { ""event"": ""up"", ""function"": ""resetBit"", ""tag"": ""Motor"" },
                { ""event"": ""tapped"", ""function"": ""changeScreen"", ""screen"": ""Drawn"" } ] } ] }";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedEvents");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Motor"", ""dataType"": ""Bool"" } ] }"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void DrawUnifiedScreen_JogButton_ReadsBackItsThreeScripts()
        {
            var drawn = Draw(JogScreen);

            var events = (JsonArray)((JsonArray)drawn["items"]!).Single()!["events"]!;
            var scripts = events.ToDictionary(entry => entry!["event"]!.GetValue<string>(), entry => entry!["script"]!.GetValue<string>());
            Assert.AreEqual("Tags(\"Motor\").Write(1);", scripts["Down"]);
            Assert.AreEqual("Tags(\"Motor\").Write(0);", scripts["Up"]);
            Assert.AreEqual("HMIRuntime.UI.SysFct.ChangeScreen(\"Drawn\", null);", scripts["Tapped"]);
        }

        /// <remarks>The backup keeps the events, so a screen with events is redrawn, and read back unchanged.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_ScreenWithEventsReadBackAndDrawnAgain_IsUnchanged()
        {
            var first = Draw(JogScreen);

            var second = Draw(first.ToJsonString());

            Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
        }

        [TestMethod]
        public void DrawUnifiedScreen_JogButton_Compiles()
        {
            Draw(JogScreen);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = report.Messages.Where(message => message.Severity == CompilationSeverity.Error && message.Path.Contains("Drawn")).ToList();
            Assert.AreEqual(0, errors.Count, string.Join(" | ", errors.Select(message => $"{message.Path}: {message.Description}")));
        }

        [TestMethod]
        public void DrawUnifiedScreen_EventTheItemDoesNotHave_ThrowsInvalidParamsAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""button"", ""events"": [ { ""event"": ""loaded"", ""function"": ""setBit"", ""tag"": ""Motor"" } ] } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "Tapped", System.StringComparison.Ordinal);
            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, "Drawn"));
        }

        /// <remarks>Openness takes broken JavaScript into a script without a word; only its syntax check sees it.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_BrokenScript_ThrowsInvalidParamsAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""button"", ""name"": ""Broken"", ""events"": [ { ""event"": ""tapped"", ""script"": ""Tags(\""Motor\"".Write(1;"" } ] } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "not valid JavaScript", System.StringComparison.Ordinal);
            var notFound = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, "Drawn"), "a broken script must leave nothing drawn");
            Assert.IsFalse(notFound.Message.Contains("TiaMcp_SyntaxCheck"), "the screen the syntax check used was left behind: " + notFound.Message);
        }

        private JsonObject Draw(string description)
        {
            return AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(description), Path.Combine(_testDirectory, "backup"));
        }
    }
}
