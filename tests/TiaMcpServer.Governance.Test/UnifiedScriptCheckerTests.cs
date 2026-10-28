using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TiaMcpServer.UnifiedScripts;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The faulty scripts are the ones found in the 2025-26 class final project (0968) on 2026-10-25, kept
    /// as they were written; each rule also has a correct script it must say nothing about.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedScriptCheckerTests
    {
        private static readonly UnifiedScriptContext Hmi = new UnifiedScriptContext(
            new Dictionary<string, string>
            {
                ["PROGRAMA MANUAL"] = "Bool", ["ROBO_AGAFAR"] = "Bool", ["MOTOR_ESTACIO_ROBOT"] = "Bool", ["PARADA_MOTOR_MAN"] = "Bool",
                ["Alarms"] = "Word", ["Count"] = "Int", ["Big"] = "LWord", ["PLATAFORMES"] = "Array [0..13] of PLATAF_ARNAU", ["@UserName"] = "WString"
            });

        /// <summary>The 0968 robot button: an assignment in the condition and a local variable for a tag.</summary>
        private const string RobotButton = "let man = Tags(\"PROGRAMA MANUAL\");\r\nlet pols = Tags(\"ROBO_AGAFAR\");\r\n\r\n\r\nif (man = true){\r\n  pols = true;\r\n}\r\n";

        [TestMethod]
        public void Check_RobotButtonOf0968_FindsTheAssignmentAndTheLocalWrite()
        {
            var findings = UnifiedScriptChecker.Check(RobotButton, Hmi);

            Assert.IsTrue(findings.Any(finding => finding.Rule == "assignment-in-condition" && finding.Line == 5 && finding.Severity == UnifiedScriptSeverity.Error), Describe(findings));
            Assert.IsTrue(findings.Any(finding => finding.Rule == "local-variable-write" && finding.Line == 6 && finding.Message.Contains("ROBO_AGAFAR")), Describe(findings));
        }

        [TestMethod]
        public void Check_SetBitWithZeroOnABoolAsIn0968_IsAWarningNamingSetTagValue()
        {
            var findings = UnifiedScriptChecker.Check("HMIRuntime.Tags.SysFct.SetBitInTag(\"MOTOR_ESTACIO_ROBOT\", 0);", Hmi);

            Assert.AreEqual("bit-function", findings.Single().Rule, Describe(findings));
            Assert.AreEqual(UnifiedScriptSeverity.Warning, findings.Single().Severity);
            StringAssert.Contains(findings.Single().Message, "SetTagValue", System.StringComparison.Ordinal);
        }

        /// <remarks><c>if (man = true)</c> is one fault, reported once.</remarks>
        [TestMethod]
        public void Check_AssignmentToAHeldTagInACondition_IsReportedOnlyAsTheAssignment()
        {
            var findings = UnifiedScriptChecker.Check("let man = Tags(\"PROGRAMA MANUAL\");\r\nif (man = true) { }", Hmi);

            CollectionAssert.AreEqual(new[] { "assignment-in-condition" }, findings.Select(finding => finding.Rule).ToArray(), Describe(findings));
        }

        [TestMethod]
        public void Check_NotFunctionAsIn0968_IsAnError()
        {
            var findings = UnifiedScriptChecker.Check("var value;\r\nlet t1 = Tags(\"Count\").Read();\r\nif (not(t1) & not(t1)) {\r\n value = true;\r\n}\r\nreturn value;", Hmi);

            Assert.AreEqual(1, findings.Count(finding => finding.Rule == "not-javascript"), Describe(findings));
            Assert.AreEqual(3, findings.First(finding => finding.Rule == "not-javascript").Line);
        }

        [TestMethod]
        [DataRow("if (a == b) { x = 1; }", DisplayName = "a comparison")]
        [DataRow("if (a === b && c !== d) { }", DisplayName = "strict comparisons")]
        [DataRow("if (a <= b || c >= d) { }", DisplayName = "orderings")]
        [DataRow("for (let i = 0; i < 3; i++) { }", DisplayName = "a for initialiser")]
        [DataRow("let p = Tags(\"Count\").Read(); let q = p + 1; Tags(\"Count\").Write(q);", DisplayName = "a tag read, then written")]
        [DataRow("HMIRuntime.Tags.SysFct.SetTagValue(\"PARADA_MOTOR_MAN\", 0);", DisplayName = "SetTagValue on a Bool")]
        [DataRow("HMIRuntime.Tags.SysFct.SetBitInTag(\"Alarms\", 15);", DisplayName = "the top bit of a Word")]
        [DataRow("let v = Tags(\"PLATAFORMES[1].POSICIO_mm\").Read();", DisplayName = "a member of an array of UDTs")]
        [DataRow("let u = Tags(\"@UserName\").Read();", DisplayName = "a system tag")]
        [DataRow("// if (a = b) and not(c)\r\nlet s = \"if (a = b) or not(c)\";", DisplayName = "faults only in a comment and a string")]
        [DataRow("function not(x) { return !x; } let y = not(1);", DisplayName = "a function the script defines as not")]
        [DataRow("while ((m = re.exec(s)) !== null) { }", DisplayName = "an assignment meant, in its own brackets")]
        [DataRow("if (f(g(a = b))) { }", DisplayName = "an assignment passed into a call")]
        [DataRow("if (list.some(function (x) { let y = x * 2; return y > 3; })) { }", DisplayName = "a function passed in the condition")]
        [DataRow("for (let i = 1; i <= 4; i++) { Tags(`Station${i}_Run`).Write(0); }", DisplayName = "a tag name built in a template")]
        [DataRow("let v = Tags(\"Count\").Read(); v = v + 1; Tags(\"Count\").Write(v);", DisplayName = "a value read, changed and written back")]
        [DataRow("// disabled", DisplayName = "only a comment")]
        [DataRow("", DisplayName = "an empty script")]
        [DataRow("HMIRuntime.Tags.SysFct.SetBitInTag(\"Big\", 63);", DisplayName = "the top bit of an LWord")]
        public void Check_CorrectScript_FindsNothing(string script)
        {
            var findings = UnifiedScriptChecker.Check(script, Hmi);

            Assert.AreEqual(0, findings.Count, Describe(findings));
        }

        [TestMethod]
        [DataRow("while (x = next()) { }", "assignment-in-condition", DisplayName = "an assignment in a while")]
        [DataRow("s = s.replace(/`'\"/g, \"\"); if (a = b) { }", "assignment-in-condition", DisplayName = "a fault after a regular expression holding quotes")]
        [DataRow("let r = a / b; let q = c / d; if (x = 1) { }", "assignment-in-condition", DisplayName = "a fault after divisions")]
        [DataRow("let p = HMIRuntime.Tags(\"Grab\"); p = true;", "local-variable-write", DisplayName = "a tag held through HMIRuntime.Tags")]
        [DataRow("if (a and b) { }", "not-javascript", DisplayName = "and")]
        [DataRow("if (a or (b)) { }", "not-javascript", DisplayName = "or")]
        [DataRow("HMIRuntime.Tags.SysFct.SetBitInTag(\"Alarms\", 16);", "bit-function", DisplayName = "a bit past a Word")]
        [DataRow("HMIRuntime.Tags.SysFct.ResetBitInTag(\"Count\", 16);", "bit-function", DisplayName = "a bit past an Int")]
        [DataRow("Tags(\"No_Such_Tag\").Write(1);", "missing-tag", DisplayName = "a tag the HMI does not have")]
        [DataRow("HMIRuntime.Tags.SysFct.SetTagValue(\"No_Such_Tag\", 1);", "missing-tag", DisplayName = "SetTagValue on a missing tag")]
        public void Check_FaultyScript_FindsTheRule(string script, string rule)
        {
            var findings = UnifiedScriptChecker.Check(script, Hmi);

            Assert.IsTrue(findings.Any(finding => finding.Rule == rule), Describe(findings));
        }

        /// <remarks>Without the HMI's tags, the rules that need them say nothing rather than guess.</remarks>
        [TestMethod]
        public void Check_NoContext_RunsOnlyTheRulesThatNeedNone()
        {
            var findings = UnifiedScriptChecker.Check("Tags(\"Anything\").Write(1); HMIRuntime.Tags.SysFct.SetBitInTag(\"Anything\", 99); if (a = 1) { }", UnifiedScriptContext.None);

            CollectionAssert.AreEqual(new[] { "assignment-in-condition" }, findings.Select(finding => finding.Rule).ToArray(), Describe(findings));
        }

        [TestMethod]
        public void Check_NotDefinedInTheGlobalDefinitions_IsNotAFault()
        {
            var findings = UnifiedScriptChecker.Check("return not(1);", Hmi, "function not(x) { return !x; }");

            Assert.AreEqual(0, findings.Count, Describe(findings));
        }

        [TestMethod]
        public void Check_TagHeldInTheGlobalDefinitions_IsFoundWhenTheScriptOverwritesIt()
        {
            var findings = UnifiedScriptChecker.Check("pols = true;", Hmi, "let pols = Tags(\"ROBO_AGAFAR\");");

            Assert.AreEqual("local-variable-write", findings.Single().Rule, Describe(findings));
        }

        [TestMethod]
        public void Check_ScriptPastTheLimit_IsNotCheckedAndSaysSo()
        {
            var findings = UnifiedScriptChecker.Check(new string(' ', UnifiedScriptChecker.MaxLength + 1), Hmi);

            Assert.AreEqual("not-checked", findings.Single().Rule);
        }

        /// <remarks>A script comes from a project, so it is data from outside: no input may make the check slow or throw.</remarks>
        [TestMethod]
        [DataRow("(", DisplayName = "deep nesting")]
        [DataRow("\"", DisplayName = "unterminated strings")]
        [DataRow("/*", DisplayName = "unterminated comments")]
        [DataRow("if (", DisplayName = "unterminated conditions")]
        [DataRow("\\", DisplayName = "backslashes")]
        public void Check_PathologicalScriptAtTheLimit_EndsQuicklyWithoutThrowing(string unit)
        {
            var script = string.Concat(Enumerable.Repeat(unit, UnifiedScriptChecker.MaxLength / unit.Length));
            var clock = Stopwatch.StartNew();

            UnifiedScriptChecker.Check(script, Hmi);

            Assert.IsTrue(clock.Elapsed.TotalSeconds < 5, $"took {clock.Elapsed.TotalSeconds:F1} s");
        }

        private static string Describe(IEnumerable<UnifiedScriptFinding> findings)
        {
            return string.Join(" | ", findings.Select(finding => $"{finding.Rule}@{finding.Line}: {finding.Message}"));
        }
    }
}
