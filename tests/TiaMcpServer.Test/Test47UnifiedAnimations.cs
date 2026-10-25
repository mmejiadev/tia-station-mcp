using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The animations of WinCC Unified screen items, drawn through <c>DrawUnifiedScreen</c>: range tables,
    /// flashing colours and scripts.
    /// </summary>
    /// <remarks>
    /// The station is a PC with WinCC Unified PC RT, as in <c>Test46UnifiedEvents</c>. The pilot light is
    /// the class one: red while the motor is stopped, green while it runs.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test47UnifiedAnimations
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string PilotLight = @"{ ""name"": ""Drawn"", ""items"": [
            { ""type"": ""circle"", ""name"": ""Lamp"", ""animations"": [
                { ""property"": ""BackColor"", ""tag"": ""Motor"", ""ranges"": [ { ""from"": 0, ""value"": ""red"" }, { ""from"": 1, ""value"": ""green"" } ] } ] } ] }";

        private const string EveryKind = @"{ ""name"": ""Drawn"", ""items"": [
            { ""type"": ""rectangle"", ""name"": ""Tank"", ""animations"": [
                { ""property"": ""BackColor"", ""tag"": ""Level"", ""ranges"": [
                    { ""from"": 0, ""to"": 20, ""value"": ""yellow"" },
                    { ""from"": 21, ""to"": 79, ""value"": ""blue"" },
                    { ""from"": 80, ""to"": 100, ""value"": ""red"", ""flashing"": { ""rate"": ""fast"", ""alternate"": ""white"" } } ] },
                { ""property"": ""Visible"", ""tag"": ""Motor"", ""ranges"": [ { ""from"": 0, ""value"": false }, { ""from"": 1, ""value"": true } ] },
                { ""property"": ""BorderColor"", ""flashing"": { ""color"": ""red"", ""alternate"": ""gray"", ""rate"": ""slow"" } } ] },
            { ""type"": ""circle"", ""name"": ""Scripted"", ""tags"": { ""BorderColor"": ""Level"" }, ""animations"": [
                { ""property"": ""BackColor"", ""script"": ""return Tags(\""Motor\"").Read() ? 0xFF00C800 : 0xFFFF0000;"", ""trigger"": [ ""Motor"" ] } ] } ] }";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedAnimations");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [
                { ""name"": ""Motor"", ""dataType"": ""Bool"" }, { ""name"": ""Level"", ""dataType"": ""Int"" } ] }"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void DrawUnifiedScreen_PilotLight_ReadsBackOneRangePerValue()
        {
            var drawn = Draw(PilotLight);

            var animation = (JsonObject)((JsonArray)((JsonArray)drawn["items"]!).Single()!["animations"]!).Single()!;
            Assert.AreEqual("Motor", animation["tag"]!.GetValue<string>());
            var ranges = (JsonArray)animation["ranges"]!;
            Assert.AreEqual(@"{""from"":0,""value"":""255, 0, 0""}", ranges[0]!.ToJsonString());
            Assert.AreEqual(@"{""from"":1,""value"":""0, 255, 0""}", ranges[1]!.ToJsonString());
        }

        /// <remarks>
        /// The backup keeps every kind drawn, so a screen with all of them is redrawn and read back
        /// unchanged. Before 2026-10-25 a range table was read back as a plain tag link.
        /// </remarks>
        [TestMethod]
        public void DrawUnifiedScreen_EveryKindReadBackAndDrawnAgain_IsUnchanged()
        {
            var first = Draw(EveryKind);

            var second = Draw(first.ToJsonString());

            Assert.AreEqual(first.ToJsonString(), second.ToJsonString());
            StringAssert.Contains(first.ToJsonString(), @"""flashing"":{""rate"":""Fast""", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void DrawUnifiedScreen_EveryKind_Compiles()
        {
            Draw(EveryKind);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            var errors = report.Messages.Where(message => message.Severity == CompilationSeverity.Error && message.Path.Contains("Drawn")).ToList();
            Assert.AreEqual(0, errors.Count, string.Join(" | ", errors.Select(message => $"{message.Path}: {message.Description}")));
        }

        /// <remarks>A description's property is matched case aside; Openness is given the property's own name.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_PropertyInAnotherCase_IsAnimatedUnderItsOwnName()
        {
            var drawn = Draw(PilotLight.Replace(@"""property"": ""BackColor""", @"""property"": ""backcolor"""));

            var animation = ((JsonArray)((JsonArray)drawn["items"]!).Single()!["animations"]!).Single()!;
            Assert.AreEqual("BackColor", animation["property"]!.GetValue<string>());
        }

        /// <remarks>An IO field's Alignment takes no dynamization, which only TIA Portal knows: the rehearsal finds it.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_PropertyThatTakesNoAnimation_ThrowsInvalidParamsAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""ioField"", ""name"": ""Field"", ""animations"": [
                { ""property"": ""Alignment"", ""script"": ""return 0;"" } ] } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "Alignment", System.StringComparison.Ordinal);
            AssertNothingDrawn();
        }

        [TestMethod]
        public void DrawUnifiedScreen_BrokenAnimationScript_ThrowsInvalidParamsAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Drawn"", ""items"": [ { ""type"": ""circle"", ""name"": ""Broken"", ""animations"": [
                { ""property"": ""BackColor"", ""script"": ""return (1;"" } ] } ] }");

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "not valid JavaScript", System.StringComparison.Ordinal);
            AssertNothingDrawn();
        }

        [TestMethod]
        public void DrawUnifiedScreen_RangesOnATagThatDoesNotExist_ThrowsInvalidStateAndDrawsNothing()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(PilotLight.Replace(@"""tag"": ""Motor""", @"""tag"": ""Nowhere"""));

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "Nowhere", System.StringComparison.Ordinal);
            AssertNothingDrawn();
        }

        private static void AssertNothingDrawn()
        {
            var notFound = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, "Drawn"), "a refused description must leave nothing drawn");
            Assert.IsFalse(notFound.Message.Contains("TiaMcp_SyntaxCheck"), "the rehearsal screen was left behind: " + notFound.Message);
        }

        private JsonObject Draw(string description)
        {
            return AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(description), Path.Combine(_testDirectory, "backup"));
        }
    }
}
