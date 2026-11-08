using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScripts;

namespace TiaMcpServer.Test
{
    /// <summary>The global script modules of a WinCC Unified HMI: read, written, replaced only when asked, broken code refused (HMI-COVERAGE E7).</summary>
    /// <remarks>On a project made here, with a PC station carrying a Unified runtime.</remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test60UnifiedScriptModules
    {
        private const string HmiPath = "PC-System_1/HMI_RT_1";

        private const string Module = "MG_Volume";

        private const string Code = "export function volume(radius, height) {\r\n  return radius * radius * Math.PI * height;\r\n}\r\n";

        private const string OtherCode = "export function volume(radius, height) {\r\n  return 0;\r\n}\r\n";

        // Its brackets balance, so only TIA Portal's own syntax check can find what is wrong with it.
        private const string BrokenPastOurChecker = "export function broken() {\r\n  return 1 +;\r\n}\r\n";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "UnifiedScriptModules");
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPc);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetUnifiedScriptModules_NoModule_IsEmpty()
        {
            var read = AssemblyHooks.SharedPortal.GetUnifiedScriptModules(HmiPath);

            Assert.AreEqual(0, read["modules"]!.AsArray().Count);
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_ANewModule_IsReadBackWithItsCode()
        {
            var written = Write(Code, replace: false);

            Assert.IsFalse(written["replaced"]!.GetValue<bool>());
            Assert.AreEqual(0, written["findings"]!.AsArray().Count, written.ToJsonString());
            StringAssert.Contains(ModuleCode(), "radius * radius", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_CodeWithAccents_IsReadBackTheSame()
        {
            Write("// Càlcul del volum: señal\r\nexport function one() {\r\n  return 1;\r\n}\r\n", replace: false);

            StringAssert.Contains(ModuleCode(), "Càlcul del volum: señal", System.StringComparison.Ordinal);
        }

        /// <remarks>An import is not valid in the function body the syntax check reads; TIA Portal keeps it in the module's global definition area.</remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_AnImportAndAPlainFunction_AreKept()
        {
            Write("import * as Other from 'Other';\r\nfunction one() {\r\n  return 1;\r\n}\r\n", replace: false);

            StringAssert.Contains(ModuleCode(), "import * as Other from 'Other';", System.StringComparison.Ordinal);
            StringAssert.Contains(ModuleCode(), "export function one()", System.StringComparison.Ordinal);
        }

        /// <remarks>TIA Portal moves the closing brace of a function written on one line (measured 2026-11-08).</remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_FunctionsOnOneLine_AreAcceptedAsTiaPortalReformatsThem()
        {
            var written = Write("export function one() { return 1; }\r\nexport function two() { return 2; }\r\n", replace: false);

            Assert.IsFalse(written["replaced"]!.GetValue<bool>());
        }

        /// <remarks>What HoldsCode assumes of TIA Portal: declarations stay functions, the rest are definitions in their order.</remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_DefinitionsAndFunctionsMixed_AreKept()
        {
            var written = Write("const LIMIT = 10;\r\nvar f = function () {\r\n  return LIMIT;\r\n};\r\nexport class Pump {\r\n  run() { return 1; }\r\n}\r\nexport async function one() {\r\n  return f();\r\n}\r\n", replace: false);

            Assert.IsFalse(written["replaced"]!.GetValue<bool>());
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_ReplaceInAnotherCase_ReplacesTheModuleHeld()
        {
            Write(Code, replace: false);

            AssemblyHooks.SharedPortal.WriteUnifiedScriptModule(HmiPath, new UnifiedScriptModuleWrite(Module.ToUpperInvariant(), OtherCode, replace: true), Path.Combine(_testDirectory, "other-case"));

            Assert.AreEqual(1, AssemblyHooks.SharedPortal.GetUnifiedScriptModules(HmiPath)["modules"]!.AsArray().Count);
            StringAssert.Contains(ModuleCode(), "return 0;", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_WhatWasReadBack_CanBeWrittenAgain()
        {
            Write(Code, replace: false);

            var written = Write(ModuleCode(), replace: true);

            Assert.IsTrue(written["replaced"]!.GetValue<bool>());
        }

        /// <remarks>TIA Portal imports this and keeps "default function ...", which does not parse (measured 2026-11-08).</remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_ADefaultExport_IsRefusedAndCreatesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Write("export default function () {\r\n  return 2;\r\n}\r\n", replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetUnifiedScriptModules(HmiPath)["modules"]!.AsArray().Count);
        }

        /// <remarks>TIA Portal replaces a module of that name without a word (measured 2026-11-08).</remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_AModuleAlreadyThere_IsRefusedAndKeepsItsCode()
        {
            Write(Code, replace: false);

            var failure = Assert.ThrowsException<PortalException>(() => Write(OtherCode, replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code, failure.Message);
            StringAssert.Contains(ModuleCode(), "radius * radius", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_ReplaceAskedFor_ReplacesItAndKeepsTheOldOneInTheBackup()
        {
            Write(Code, replace: false);
            var backup = Path.Combine(_testDirectory, "replace-backup");

            var written = AssemblyHooks.SharedPortal.WriteUnifiedScriptModule(HmiPath, new UnifiedScriptModuleWrite(Module, OtherCode, replace: true), backup);

            Assert.IsTrue(written["replaced"]!.GetValue<bool>());
            StringAssert.Contains(ModuleCode(), "return 0;", System.StringComparison.Ordinal);
            StringAssert.Contains(File.ReadAllText(Path.Combine(backup, Module + UnifiedScriptModuleRules.ScriptExtension)), "radius * radius", System.StringComparison.Ordinal);
        }

        /// <remarks>TIA Portal imported this and compiled it with no error (measured 2026-11-08), so it has to be refused here.</remarks>
        [TestMethod]
        public void WriteUnifiedScriptModule_BrokenJavaScript_IsRefusedAndCreatesNothing()
        {
            Assert.IsFalse(UnifiedScriptChecker.Check(BrokenPastOurChecker, UnifiedScriptContext.None).Any(finding => finding.Severity == UnifiedScriptSeverity.Error));

            var failure = Assert.ThrowsException<PortalException>(() => Write(BrokenPastOurChecker, replace: false));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code, failure.Message);
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.GetUnifiedScriptModules(HmiPath)["modules"]!.AsArray().Count);
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_ASyntaxError_LeavesNoScratchScreen()
        {
            var before = ScreenCount();

            Assert.ThrowsException<PortalException>(() => Write(BrokenPastOurChecker, replace: false));

            Assert.AreEqual(before, ScreenCount());
        }

        [TestMethod]
        public void WriteUnifiedScriptModule_AWriteThatWorks_LeavesNoScratchScreen()
        {
            var before = ScreenCount();

            Write(Code, replace: false);

            Assert.AreEqual(before, ScreenCount());
        }

        private JsonObject Write(string code, bool replace)
        {
            return AssemblyHooks.SharedPortal.WriteUnifiedScriptModule(HmiPath, new UnifiedScriptModuleWrite(Module, code, replace), Path.Combine(_testDirectory, "backup"));
        }

        private static int ScreenCount()
        {
            return AssemblyHooks.SharedPortal.CheckUnifiedScripts(HmiPath, null)["screens"]!.GetValue<int>();
        }

        private static string ModuleCode()
        {
            return AssemblyHooks.SharedPortal.GetUnifiedScriptModules(HmiPath)["modules"]!.AsArray().Single(module => module!["name"]!.GetValue<string>() == Module)!["code"]!.GetValue<string>();
        }
    }
}
