using System;
using System.Linq;
using TiaMcpServer.Scl;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Each rule rests on a measurement of 2026-11-18 in PLCSIM Advanced V8 (docs/PEEK-POKE.md, "Measured").</remarks>
    [TestClass]
    public sealed class SclMemoryAccessRulesTests
    {
        /// <summary>A program holding DB 2, standard, and DB 3, optimized.</summary>
        private static readonly ProgramDataBlocks OptimizedDb3 = ProgramDataBlocks.Of(new[] { 2, 3 }, new[] { 3 });

        [TestMethod]
        public void Check_AReadOfAStandardDb_FindsNothing()
        {
            var findings = SclMemoryAccessRules.Check("#w := PEEK_WORD(AREA := 16#84, DBNUMBER := 2, BYTEOFFSET := 0);", OptimizedDb3);

            Assert.AreEqual(0, findings.Count);
        }

        /// <remarks>Measured: it compiles, and returns 0 with ENO FALSE.</remarks>
        [TestMethod]
        public void Check_AReadOfAnOptimizedDbByNumber_IsAnError()
        {
            var findings = SclMemoryAccessRules.Check("#w := PEEK_WORD(AREA := 16#84, DBNUMBER := 3, BYTEOFFSET := 0);", OptimizedDb3);

            Assert.AreEqual("peek-poke-optimized-db", findings.Single().Rule);
            Assert.IsTrue(findings.Single().IsError);
        }

        [TestMethod]
        [DataRow("#b := PEEK(AREA := 16#83, DBNUMBER := 1, BYTEOFFSET := 0);", "peek-poke-dbnumber-not-zero")]
        [DataRow("#b := PEEK(AREA := B#16#90, DBNUMBER := 0, BYTEOFFSET := 0);", "peek-poke-unknown-area")]
        [DataRow("#b := PEEK(AREA := 16#84, DBNUMBER := 2, BYTEOFFSET := 65539);", "peek-poke-offset-over-16-bit")]
        [DataRow("POKE(AREA := 16#82, DBNUMBER := 0, BYTEOFFSET := 0, VALUE := #b);", "poke-writes-outputs")]
        [DataRow("POKE_BLK(AREA_SRC := 16#84, DBNUMBER_SRC := 2, BYTEOFFSET_SRC := 0, AREA_DEST := 16#82, DBNUMBER_DEST := 0, BYTEOFFSET_DEST := 0, COUNT := 2);", "poke-writes-outputs")]
        public void Check_AConstantThatFailsQuietly_IsFound(string source, string rule)
        {
            var findings = SclMemoryAccessRules.Check(source, ProgramDataBlocks.Unknown);

            CollectionAssert.Contains(findings.Select(finding => finding.Rule).ToList(), rule);
        }

        /// <remarks>Measured: GET_ERR_ID is 0 after a failed PEEK; ENO is FALSE.</remarks>
        [TestMethod]
        public void Check_GetErrIdBesideAPeekWithoutEno_WarnsToReadEno()
        {
            var findings = SclMemoryAccessRules.Check("#b := PEEK(AREA := 16#84, DBNUMBER := #n, BYTEOFFSET := 0);\n#id := GET_ERR_ID();", ProgramDataBlocks.Unknown);

            Assert.AreEqual("peek-poke-eno-check", findings.Single().Rule);
            Assert.AreEqual(1, findings.Single().Line);
        }

        [TestMethod]
        public void Check_APeekReadingEno_IsNotWarned()
        {
            var findings = SclMemoryAccessRules.Check("#b := PEEK(AREA := 16#84, DBNUMBER := #n, BYTEOFFSET := 0, ENO => #ok);\n#id := GET_ERR_ID();", ProgramDataBlocks.Unknown);

            Assert.AreEqual(0, findings.Count);
        }

        /// <remarks>A value known only at run time is not judged: nothing said is not a pass.</remarks>
        [TestMethod]
        public void Check_ComputedArguments_AreNotJudged()
        {
            var findings = SclMemoryAccessRules.Check("#w := PEEK_WORD(AREA := #area, DBNUMBER := \"Cfg\".Number, BYTEOFFSET := #i * 2);", OptimizedDb3);

            Assert.AreEqual(0, findings.Count);
        }

        /// <remarks>Found in review: the hex literal inside an expression was read as the whole argument.</remarks>
        [TestMethod]
        [DataRow("#w := PEEK_WORD(AREA := 16#84, DBNUMBER := #base + 16#3, BYTEOFFSET := 0);")]
        [DataRow("#w := PEEK_WORD(AREA := 16#84, DBNUMBER := 2, BYTEOFFSET := 2 * 16#8000);")]
        public void Check_AnExpressionHoldingALiteral_IsNotJudged(string source)
        {
            var findings = SclMemoryAccessRules.Check(source, OptimizedDb3);

            Assert.AreEqual(0, findings.Count);
        }

        [TestMethod]
        [DataRow("16#FFFFFFFF")]
        [DataRow("2#1_0000_0000_0000_0011")]
        [DataRow("8#200003")]
        public void Check_AnOffsetPast65535InAnyRadix_IsWarned(string offset)
        {
            var findings = SclMemoryAccessRules.Check($"#b := PEEK(AREA := 16#84, DBNUMBER := 2, BYTEOFFSET := {offset});", ProgramDataBlocks.Unknown);

            Assert.AreEqual("peek-poke-offset-over-16-bit", findings.Single().Rule);
        }

        [TestMethod]
        public void Check_APokeBlkIntoAnOptimizedDb_IsAnError()
        {
            var findings = SclMemoryAccessRules.Check("POKE_BLK(AREA_SRC := 16#83, DBNUMBER_SRC := 0, BYTEOFFSET_SRC := 0, AREA_DEST := 16#84, DBNUMBER_DEST := 3, BYTEOFFSET_DEST := 0, COUNT := 4);", OptimizedDb3);

            Assert.AreEqual("peek-poke-optimized-db", findings.Single().Rule);
        }

        [TestMethod]
        public void Check_GetErrIdInAnotherBlock_DoesNotWarnThisOne()
        {
            var source = string.Join(
                Environment.NewLine,
                "FUNCTION_BLOCK \"A\"",
                "BEGIN",
                "#id := GET_ERR_ID();",
                "END_FUNCTION_BLOCK",
                "FUNCTION \"B\" : Void",
                "BEGIN",
                "#b := PEEK(AREA := 16#84, DBNUMBER := #n, BYTEOFFSET := 0);",
                "END_FUNCTION");

            var findings = SclMemoryAccessRules.Check(source, ProgramDataBlocks.Unknown);

            Assert.AreEqual(0, findings.Count);
        }

        /// <remarks>Found in review: the apostrophe in the name opened a string that hid the call.</remarks>
        [TestMethod]
        public void Check_AnApostropheInAQuotedName_HidesNothing()
        {
            var findings = SclMemoryAccessRules.Check("#x := \"Tank's level\"; #b := PEEK(AREA := 16#83, DBNUMBER := 1, BYTEOFFSET := 0); #m := 'it''s';", ProgramDataBlocks.Unknown);

            Assert.AreEqual("peek-poke-dbnumber-not-zero", findings.Single().Rule);
        }

        /// <remarks>Found in review: the inner PEEK's ENO was taken for the POKE's.</remarks>
        [TestMethod]
        public void Check_ANestedCallsEno_IsNotTheOuterCalls()
        {
            var findings = SclMemoryAccessRules.Check("POKE(AREA := 16#84, DBNUMBER := 2, BYTEOFFSET := 0, VALUE := PEEK(AREA := 16#83, DBNUMBER := 0, BYTEOFFSET := 0, ENO => #ok)); #id := GET_ERR_ID();", ProgramDataBlocks.Unknown);

            StringAssert.StartsWith(findings.Single(finding => finding.Rule == "peek-poke-eno-check").Message, "POKE:", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ACallInAString_IsNotRead()
        {
            var findings = SclMemoryAccessRules.Check("#msg := 'POKE(AREA := 16#82) (* ';" + Environment.NewLine + "#b := PEEK(AREA := 16#83, DBNUMBER := 1, BYTEOFFSET := 0); // *)", ProgramDataBlocks.Unknown);

            Assert.AreEqual("peek-poke-dbnumber-not-zero", findings.Single().Rule);
            Assert.AreEqual(2, findings.Single().Line);
        }

        [TestMethod]
        [DataRow("#x := 1; // PEEK(AREA := 16#84)", false)]
        [DataRow("#b := peek(area := 16#84, dbnumber := 1, byteoffset := 0);", true)]
        public void HasCalls_OnlyOutsideComments_IsTrue(string source, bool expected)
        {
            Assert.AreEqual(expected, SclMemoryAccessRules.HasCalls(source));
        }

        [TestMethod]
        public void Check_ACallInAComment_IsNotRead()
        {
            var findings = SclMemoryAccessRules.Check("// PEEK(AREA := 16#83, DBNUMBER := 1, BYTEOFFSET := 0)\n(* POKE(AREA := 16#82) *)\n#x := 1;", ProgramDataBlocks.Unknown);

            Assert.AreEqual(0, findings.Count);
        }

        [TestMethod]
        public void Check_LinesAfterAComment_KeepTheirNumbers()
        {
            var findings = SclMemoryAccessRules.Check("(* one\ntwo *)\n#b := PEEK(AREA := 16#83, DBNUMBER := 1, BYTEOFFSET := 0);", ProgramDataBlocks.Unknown);

            Assert.AreEqual(3, findings.Single().Line);
        }

        /// <remarks>Measured (M6): a DB that does not exist, read by number, returns 0 with ENO FALSE and the CPU runs on.</remarks>
        [TestMethod]
        public void Check_AConstantDbTheProgramDoesNotHold_IsAnError()
        {
            var findings = SclMemoryAccessRules.Check("#w := PEEK_WORD(AREA := 16#84, DBNUMBER := 7, BYTEOFFSET := 0);", OptimizedDb3);

            Assert.AreEqual("peek-poke-missing-db", findings.Single().Rule);
            Assert.IsTrue(findings.Single().IsError);
        }

        /// <remarks>Not knowing the program's DBs is not knowing that one is missing.</remarks>
        [TestMethod]
        public void Check_AConstantDbWhenTheProgramIsUnknown_IsNotJudged()
        {
            var findings = SclMemoryAccessRules.Check("#w := PEEK_WORD(AREA := 16#84, DBNUMBER := 7, BYTEOFFSET := 0);", ProgramDataBlocks.Unknown);

            Assert.AreEqual(0, findings.Count);
        }

        [TestMethod]
        public void Check_APokeBlkFromADbTheProgramDoesNotHold_NamesThatDb()
        {
            var findings = SclMemoryAccessRules.Check("POKE_BLK(AREA_SRC := 16#84, DBNUMBER_SRC := 9, BYTEOFFSET_SRC := 0, AREA_DEST := 16#84, DBNUMBER_DEST := 2, BYTEOFFSET_DEST := 0, COUNT := 4);", OptimizedDb3);

            Assert.AreEqual("peek-poke-missing-db", findings.Single().Rule);
            StringAssert.Contains(findings.Single().Message, "DB 9 ", StringComparison.Ordinal);
        }

        /// <remarks>The help lists 16#1 for the PEEKs only, 16#2 for POKE and POKE_BOOL only, neither for POKE_BLK; the others compile (M8).</remarks>
        [TestMethod]
        [DataRow("#b := PEEK(AREA := 16#2, DBNUMBER := 0, BYTEOFFSET := 0);")]
        [DataRow("#w := PEEK_WORD(AREA := B#16#2, DBNUMBER := 0, BYTEOFFSET := 0);")]
        [DataRow("POKE(AREA := 16#1, DBNUMBER := 0, BYTEOFFSET := 0, VALUE := #b);")]
        [DataRow("POKE_BOOL(AREA := 16#1, DBNUMBER := 0, BYTEOFFSET := 0, BITOFFSET := 0, VALUE := #x);")]
        [DataRow("POKE_BLK(AREA_SRC := 16#1, DBNUMBER_SRC := 0, BYTEOFFSET_SRC := 0, AREA_DEST := 16#83, DBNUMBER_DEST := 0, BYTEOFFSET_DEST := 0, COUNT := 2);")]
        public void Check_APeripheralAreaTheHelpDoesNotListForTheCall_IsWarned(string source)
        {
            var findings = SclMemoryAccessRules.Check(source, ProgramDataBlocks.Unknown);

            var peripheral = findings.Single(finding => finding.Rule == "peek-poke-peripheral");
            Assert.IsFalse(peripheral.IsError);
            StringAssert.Contains(peripheral.Message, "not measured", StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow("#b := PEEK(AREA := 16#1, DBNUMBER := 0, BYTEOFFSET := 0);")]
        [DataRow("POKE(AREA := 16#2, DBNUMBER := 0, BYTEOFFSET := 0, VALUE := #b);")]
        public void Check_APeripheralAreaTheHelpListsForTheCall_IsNotWarnedAsPeripheral(string source)
        {
            var findings = SclMemoryAccessRules.Check(source, ProgramDataBlocks.Unknown);

            Assert.IsFalse(findings.Any(finding => finding.Rule == "peek-poke-peripheral"));
        }

        /// <remarks>Only these need the project read: a DB named in a variable, or with an area that is not 16#84, is not.</remarks>
        [TestMethod]
        public void NamedDataBlocks_ConstantDbsWithTheDbArea_AreListedOnce()
        {
            var source = string.Join(
                Environment.NewLine,
                "#a := PEEK(AREA := 16#84, DBNUMBER := 3, BYTEOFFSET := 0);",
                "#b := PEEK(AREA := 16#84, DBNUMBER := 3, BYTEOFFSET := 1);",
                "POKE_BLK(AREA_SRC := 16#83, DBNUMBER_SRC := 0, BYTEOFFSET_SRC := 0, AREA_DEST := 16#84, DBNUMBER_DEST := 5, BYTEOFFSET_DEST := 0, COUNT := 2);",
                "#c := PEEK(AREA := #area, DBNUMBER := 7, BYTEOFFSET := 0);",
                "#d := PEEK(AREA := 16#84, DBNUMBER := #n, BYTEOFFSET := 0);",
                "// #e := PEEK(AREA := 16#84, DBNUMBER := 11, BYTEOFFSET := 0);");

            var named = SclMemoryAccessRules.NamedDataBlocks(source);

            CollectionAssert.AreEquivalent(new[] { 3L, 5L }, named.ToList());
        }

        /// <remarks>Before the write, for the plan: what the text shows is found, what needs the project is not guessed.</remarks>
        [TestMethod]
        public void CheckText_ASourceWithAProjectAndATextFinding_FindsOnlyTheTextOne()
        {
            var source = "#a := PEEK(AREA := 16#84, DBNUMBER := 7, BYTEOFFSET := 0);\n#b := PEEK(AREA := 16#83, DBNUMBER := 1, BYTEOFFSET := 0);";

            var findings = SclMemoryAccessRules.CheckText(source);

            Assert.AreEqual("peek-poke-dbnumber-not-zero", findings.Single().Rule);
        }
    }
}
