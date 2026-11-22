using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedScripts;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Test
{
    /// <summary>Calling global script modules from screens, and the event functions that read a tag and write it back.</summary>
    /// <remarks>
    /// On a new PC station with WinCC Unified PC RT. Measured 2026-11-18: TIA Portal compiles a call to a function a
    /// module lacks, or to a module not imported, without a word, so the checks here are this server's.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test75UnifiedScriptCalls
    {
        private const string StationName = "PC-System_1";
        private const string HmiPath = "PC-System_1/HMI_RT_1";
        private const string Lib = "export function f(a) {\n  return a + 1;\n}\n";

        private static readonly string[] ExpectedFindings = { "NoFunction:missing-module-function", "NotImported:module-not-imported" };

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedScriptCalls");
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPcV21);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>
        /// Measured 2026-11-18: the syntax check's throw-away screen, the first screen of a new HMI, became its start
        /// screen, and its deletion left the HMI failing to compile on a start screen that did not exist.
        /// </remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_OnAnHmiWithNoScreen_LeavesNoStartScreenBehind()
        {
            WriteModule("Lib", Lib);

            var report = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath);

            Assert.IsFalse(report.Messages.Any(message => message.Description.Contains("Start screen")), string.Join(" | ", report.Messages.Select(message => message.Description)));
        }

        [TestMethod]
        public void CheckUnifiedScripts_CallsToAModule_FindsWhatTiaPortalCompilesUnseen()
        {
            WriteModule("Lib", Lib);
            Draw(@"{ ""type"": ""button"", ""name"": ""Good"", ""events"": [ { ""event"": ""tapped"", ""function"": ""callModule"", ""module"": ""Lib"", ""call"": ""f"", ""arguments"": [ 1 ] } ] },
                { ""type"": ""button"", ""name"": ""NoFunction"", ""events"": [ { ""event"": ""tapped"", ""script"": ""lib.g(1);"", ""globalDefinitions"": ""import * as lib from 'Lib';"" } ] }");

            // On a screen of its own: the global definitions are the screen's, so beside the others it would be imported.
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Bare"", ""items"": [
                { ""type"": ""button"", ""name"": ""NotImported"", ""events"": [ { ""event"": ""tapped"", ""script"": ""Lib.f(1);"" } ] } ] }"), Backup());

            var checkedScripts = AssemblyHooks.SharedPortal.CheckUnifiedScripts(HmiPath, null);
            var findings = checkedScripts["findings"]!.AsArray()
                .Select(finding => $"{finding!["item"]}:{finding["rule"]}")
                .ToList();

            CollectionAssert.AreEquivalent(ExpectedFindings, findings, checkedScripts.ToJsonString());
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_CallingAFunctionAnotherModuleLacks_ThrowsInvalidParams()
        {
            WriteModule("Lib", Lib);

            var failure = Assert.ThrowsException<PortalException>(() => WriteModule("Uses", "import * as lib from 'Lib';\nexport function u() {\n  return lib.nope(1);\n}\n"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "nope", StringComparison.Ordinal);
        }

        /// <remarks>Each goes through TIA Portal's syntax check when drawn, and is read back as written.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_ValueFunctions_PassTheSyntaxCheckAndReadBack()
        {
            AssemblyHooks.SharedPortal.CreateUnifiedTags(HmiPath, UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Speed"", ""dataType"": ""Int"" }, { ""name"": ""Lamp"", ""dataType"": ""Bool"" } ] }"));

            var drawn = Draw(@"{ ""type"": ""button"", ""name"": ""Up"", ""events"": [ { ""event"": ""tapped"", ""function"": ""increaseValue"", ""tag"": ""Speed"", ""value"": 5 } ] },
                { ""type"": ""button"", ""name"": ""Down"", ""events"": [ { ""event"": ""tapped"", ""function"": ""decreaseValue"", ""tag"": ""Speed"", ""value"": 5 } ] },
                { ""type"": ""button"", ""name"": ""Toggle"", ""events"": [ { ""event"": ""tapped"", ""function"": ""invertBit"", ""tag"": ""Lamp"" } ] }");

            var scripts = drawn["items"]!.AsArray().Select(item => item!["events"]![0]!["script"]!.GetValue<string>()).ToList();
            StringAssert.Contains(scripts[0], "Read() + 5", StringComparison.Ordinal);
            StringAssert.Contains(scripts[2], "!Tags(\"Lamp\").Read()", StringComparison.Ordinal);
        }

        /// <remarks>Measured 2026-11-18: a screen has one area for its events' global definitions; the last item written used to win.</remarks>
        [TestMethod]
        public void DrawUnifiedScreen_ItemsWithTheirOwnDefinitions_EachKeepsThemAll()
        {
            var drawn = Draw(@"{ ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""script"": ""x();"", ""globalDefinitions"": ""let a = 1;"" } ] },
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""script"": ""y();"", ""globalDefinitions"": ""let b = 2;"" } ] },
                { ""type"": ""button"", ""name"": ""C"", ""events"": [ { ""event"": ""tapped"", ""script"": ""z();"" } ] }");

            var globals = drawn["items"]!.AsArray().Select(item => item!["events"]![0]!["globalDefinitions"]?.GetValue<string>() ?? string.Empty).ToList();

            Assert.IsTrue(globals.All(each => each.Contains("let a = 1;") && each.Contains("let b = 2;")), string.Join(" | ", globals));
        }

        [TestMethod]
        public void EditUnifiedScreen_DrawingAnItemWithDefinitions_KeepsThoseAlreadyThere()
        {
            Draw(@"{ ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""script"": ""x();"", ""globalDefinitions"": ""let a = 1;"" } ] }");

            AssemblyHooks.SharedPortal.EditUnifiedScreen(HmiPath, UnifiedScreenEditReader.Parse(@"{ ""screen"": ""Calls"", ""draw"": [
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""script"": ""y();"", ""globalDefinitions"": ""let b = 2;"" } ] } ] }"), Backup());

            var a = AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, "Calls")["items"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "A")!;
            var globals = a["events"]![0]!["globalDefinitions"]!.GetValue<string>();
            Assert.IsTrue(globals.Contains("let a = 1;") && globals.Contains("let b = 2;"), globals);
        }

        /// <remarks>
        /// Measured 2026-11-19: with an import in a screen's area, a script's syntax check ends TIA Portal's session, so
        /// scripts are checked without imports and the area is written once, after the items are drawn.
        /// </remarks>
        [TestMethod]
        public void DrawUnifiedScreen_TwoButtonsImportingAModule_DrawsAndKeepsTheImport()
        {
            WriteModule("Lib", Lib);

            var drawn = Draw(@"{ ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""function"": ""callModule"", ""module"": ""Lib"", ""call"": ""f"", ""arguments"": [ 1 ] } ] },
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""function"": ""callModule"", ""module"": ""Lib"", ""call"": ""f"", ""arguments"": [ 2 ] } ] }");

            var globals = drawn["items"]!.AsArray().Select(item => item!["events"]![0]!["globalDefinitions"]?.GetValue<string>() ?? string.Empty).ToList();
            Assert.IsTrue(globals.All(each => each.Contains("import * as Lib from 'Lib';")), string.Join(" | ", globals));
        }

        [TestMethod]
        public void EditUnifiedScreen_OnAScreenImportingAModule_DrawsAnotherButton()
        {
            WriteModule("Lib", Lib);
            Draw(@"{ ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""function"": ""callModule"", ""module"": ""Lib"", ""call"": ""f"" } ] }");

            AssemblyHooks.SharedPortal.EditUnifiedScreen(HmiPath, UnifiedScreenEditReader.Parse(@"{ ""screen"": ""Calls"", ""draw"": [
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""script"": ""HMIRuntime.Trace('b');"" } ] } ] }"), Backup());

            var b = AssemblyHooks.SharedPortal.GetUnifiedScreen(HmiPath, "Calls")["items"]!.AsArray().Single(item => item!["name"]!.GetValue<string>() == "B")!;
            StringAssert.Contains(b["events"]![0]!["globalDefinitions"]!.GetValue<string>(), "import * as Lib from 'Lib';", StringComparison.Ordinal);
        }

        private void WriteModule(string name, string code)
        {
            AssemblyHooks.SharedPortal.WriteUnifiedScriptModule(HmiPath, new UnifiedScriptModuleWrite(name, code, false), Backup());
        }

        private JsonObject Draw(string items)
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Calls"", ""startScreen"": true, ""items"": [ " + items + " ] }");

            return AssemblyHooks.SharedPortal.DrawUnifiedScreen(HmiPath, screen, Backup());
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
