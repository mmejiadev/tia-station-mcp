using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>Checking WinCC Unified scripts in a project: the faults of the 0968 final project, drawn here, found again.</summary>
    /// <remarks>
    /// TIA Portal draws and compiles every one of these scripts without a word; that is why the check exists.
    /// The scripts are the ones found in the class project on 2026-10-25.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test52UnifiedScriptCheck
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Faulty = @"{ ""name"": ""Faulty"", ""items"": [
            { ""type"": ""button"", ""name"": ""Robot"", ""events"": [
                { ""event"": ""down"", ""script"": ""let man = Tags(\""Manual\"");\r\nlet pols = Tags(\""Grab\"");\r\nif (man = true){\r\n  pols = true;\r\n}"" } ] },
            { ""type"": ""button"", ""name"": ""Motor"", ""events"": [
                { ""event"": ""up"", ""script"": ""HMIRuntime.Tags.SysFct.SetBitInTag(\""Motor_On\"", 0);"" } ] },
            { ""type"": ""rectangle"", ""name"": ""Lamp"", ""animations"": [
                { ""property"": ""Visible"", ""script"": ""let t = Tags(\""Manual\"").Read();\r\nreturn not(t);"", ""trigger"": [ ""Manual"" ] } ] } ] }";

        private const string Clean = @"{ ""name"": ""Clean"", ""items"": [
            { ""type"": ""button"", ""name"": ""Start"", ""events"": [
                { ""event"": ""down"", ""script"": ""HMIRuntime.Tags.SysFct.SetTagValue(\""Motor_On\"", 1);"" },
                { ""event"": ""up"", ""function"": ""resetBit"", ""tag"": ""Grab"" } ] },
            { ""type"": ""rectangle"", ""name"": ""Lamp"", ""animations"": [
                { ""property"": ""Visible"", ""script"": ""return !Tags(\""Manual\"").Read();"", ""trigger"": [ ""Manual"" ] } ] } ] }";

        private static readonly string[] FaultsOf0968 = { "Robot:assignment-in-condition", "Robot:local-variable-write", "Motor:bit-function", "Lamp:not-javascript" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedScriptCheck");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [
                { ""name"": ""Manual"", ""dataType"": ""Bool"" }, { ""name"": ""Grab"", ""dataType"": ""Bool"" }, { ""name"": ""Motor_On"", ""dataType"": ""Bool"" } ] }"));
            Draw(Faulty);
            Draw(Clean);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CheckUnifiedScripts_FaultyScreen_FindsEveryFaultOf0968()
        {
            var findings = Findings("Faulty");

            CollectionAssert.AreEquivalent(
                FaultsOf0968,
                findings.Select(finding => $"{finding!["item"]!.GetValue<string>()}:{finding["rule"]!.GetValue<string>()}").ToArray(),
                findings.ToJsonString());
        }

        [TestMethod]
        public void CheckUnifiedScripts_Finding_NamesWhereAndTheLine()
        {
            var assignment = Findings("Faulty").Single(finding => finding!["rule"]!.GetValue<string>() == "assignment-in-condition")!;

            Assert.AreEqual("Faulty", assignment["screen"]!.GetValue<string>());
            Assert.IsTrue(string.Equals("event down", assignment["where"]!.GetValue<string>(), System.StringComparison.OrdinalIgnoreCase), assignment.ToJsonString());
            Assert.AreEqual(3, assignment["line"]!.GetValue<int>());
            Assert.AreEqual("error", assignment["severity"]!.GetValue<string>());
        }

        [TestMethod]
        public void CheckUnifiedScripts_CleanScreen_FindsNothing()
        {
            var findings = Findings("Clean");

            Assert.AreEqual(0, findings.Count, findings.ToJsonString());
        }

        [TestMethod]
        public void CheckUnifiedScripts_EveryScreen_ChecksBoth()
        {
            var result = AssemblyHooks.SharedPortal.CheckUnifiedScripts(HmiPath, null);

            Assert.AreEqual(2, result["screens"]!.GetValue<int>());
            Assert.AreEqual(4, result["findings"]!.AsArray().Count, result.ToJsonString());
        }

        [TestMethod]
        public void CheckUnifiedScripts_ScreenThatDoesNotExist_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CheckUnifiedScripts(HmiPath, "Nowhere"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code, failure.Message);
            StringAssert.Contains(failure.Message, "Faulty", System.StringComparison.Ordinal);
        }

        private static JsonArray Findings(string screen)
        {
            return AssemblyHooks.SharedPortal.CheckUnifiedScripts(HmiPath, screen)["findings"]!.AsArray();
        }

        private void Draw(string description)
        {
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(description), Path.Combine(_testDirectory, "backup"));
        }
    }
}
