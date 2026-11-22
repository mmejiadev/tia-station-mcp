using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedScripts;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Calls into the HMI's global script modules. Measured 2026-11-18 on TIA Portal V21: a call to a function a
    /// module lacks, to a module that does not exist, or to a module not imported, all compile with no error.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedScriptCallRulesTests
    {
        private static readonly UnifiedScriptContext WithLib = new UnifiedScriptContext(
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyCollection<string>> { ["Lib"] = new[] { "f" } });

        [TestMethod]
        public void Check_AnImportedModulesFunction_FindsNothing()
        {
            var findings = UnifiedScriptChecker.Check("lib.f(1);", WithLib, "import * as lib from 'Lib';");

            Assert.AreEqual(0, findings.Count);
        }

        [TestMethod]
        public void Check_AFunctionTheModuleLacks_IsAnError()
        {
            var findings = UnifiedScriptChecker.Check("lib.g(1);", WithLib, "import * as lib from 'Lib';");

            Assert.AreEqual("missing-module-function", findings.Single().Rule);
            Assert.AreEqual(UnifiedScriptSeverity.Error, findings.Single().Severity);
        }

        /// <remarks>The help: a module is reached only through an import; without one its name is undefined in runtime.</remarks>
        [TestMethod]
        public void Check_AModuleCalledWithoutAnImport_IsAnError()
        {
            var findings = UnifiedScriptChecker.Check("Lib.f(1);", WithLib);

            Assert.AreEqual("module-not-imported", findings.Single().Rule);
        }

        [TestMethod]
        public void Check_AnImportOfAModuleTheHmiLacks_IsAnError()
        {
            var findings = UnifiedScriptChecker.Check("import * as x from 'Nope';", UnifiedScriptContext.None);

            Assert.AreEqual(0, findings.Count, "without the HMI's modules nothing is judged");
            Assert.AreEqual("missing-module", UnifiedScriptChecker.Check("import * as x from 'Nope';", WithLib).Single().Rule);
        }

        [TestMethod]
        public void Check_ANamedImportOfAFunctionTheModuleLacks_IsAnError()
        {
            var findings = UnifiedScriptChecker.Check("import { f, g as h } from 'Lib';", WithLib);

            Assert.AreEqual("missing-module-function", findings.Single().Rule);
            StringAssert.Contains(findings.Single().Message, "'g'", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ACallOnAnObjectNotAModule_IsNotJudged()
        {
            var findings = UnifiedScriptChecker.Check("HMIRuntime.Trace('x'); Screen.Items('a').Visible = true;", WithLib);

            Assert.AreEqual(0, findings.Count);
        }

        [TestMethod]
        public void Functions_TopLevelAndExported_AreOffered_NestedAreNot()
        {
            var functions = UnifiedScriptExports.Functions("export function a() { function inner() {} }\nfunction b() {}\nexport async function c() {}");

            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, functions.ToList());
        }
    }
}
