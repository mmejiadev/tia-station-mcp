using System;
using TiaMcpServer.Scl;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// In Workshop Mode a supervisor confirms the plan before the SCL is written, so what the text alone shows of its PEEK and
    /// POKE calls has to be in the plan, not only in the answer that comes after the write.
    /// </remarks>
    [TestClass]
    public sealed class SclMemoryAccessSummaryTests
    {
        private const string PokeToOutputs = "POKE(AREA := 16#82, DBNUMBER := 0, BYTEOFFSET := 4, VALUE := #b);";

        [TestMethod]
        public void ForPlan_NoFindings_AddsNothing()
        {
            var suffix = SclMemoryAccessSummary.ForPlan(SclMemoryAccessRules.CheckText("#x := 1;"));

            Assert.AreEqual(string.Empty, suffix);
        }

        [TestMethod]
        public void ForPlan_APokeToOutputs_NamesTheRuleAndTheLine()
        {
            var suffix = SclMemoryAccessSummary.ForPlan(SclMemoryAccessRules.CheckText("#x := 1;\n" + PokeToOutputs));

            StringAssert.Contains(suffix, "line 2 warning poke-writes-outputs", StringComparison.Ordinal);
        }

        /// <remarks>The plan a person reads is its one-line description; the finding has to be in it.</remarks>
        [TestMethod]
        public void PlanDescription_ARequestCarryingTheFindings_ShowsThemBeforeTheWrite()
        {
            var value = "FUNCTION \"FC_Out\" : Void" + SclMemoryAccessSummary.ForPlan(SclMemoryAccessRules.CheckText(PokeToOutputs));
            var request = new ChangeRequest("WriteScl", "PLC_0", value, "test");

            var plan = new ChangePlan(PlanId.Create(), request, OperationMode.Workshop, DateTimeOffset.UtcNow.AddMinutes(5));

            StringAssert.Contains(plan.ToString(), "poke-writes-outputs", StringComparison.Ordinal);
        }
    }
}
