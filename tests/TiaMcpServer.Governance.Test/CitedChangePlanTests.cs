using System;
using System.Collections.Generic;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Stage 2 of the knowledge layer attaches cited hardware context to the plan every write
    /// already produces. Two properties make that safe rather than merely nice, and each has a test
    /// here that names it: the citations reach the plan **through the guard**, so no tool can forget
    /// to ask; and a lookup that fails **never stops a write**, because a citation informs a change
    /// and does not gate one.
    /// </remarks>
    [TestClass]
    public class CitedChangePlanTests
    {
        private const string AllowedTarget = "PLC_0/Blocks/FB_Station";
        private const string ForbiddenTarget = "PLC_1/Blocks/FB_Station";

        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void Propose_IndexHasSomethingToSay_PlanCarriesTheCitation()
        {
            var lookup = new StubHardwareLookup(HardwareContext.Cited(new[] { Citation() }));

            var outcome = GuardWith(lookup).Propose(Request(AllowedTarget), () => "written", Now);

            Assert.IsNotNull(outcome.Plan);
            Assert.AreEqual(HardwareContextOutcome.Cited, outcome.Plan!.Documentation.Outcome);
            Assert.AreEqual(47, outcome.Plan.Documentation.Citations[0].Page);
        }

        [TestMethod]
        public void Propose_Always_AsksAboutWhatIsBeingWrittenAndWhereItGoes()
        {
            // The question is the change's own words. Asking about the tool name instead would
            // return whatever the corpus says about the word "WriteScl", which is nothing useful
            // and, worse, might rank something irrelevant highly enough to quote.
            var lookup = new StubHardwareLookup(HardwareContext.NotFound());

            GuardWith(lookup).Propose(Request(AllowedTarget), () => "written", Now);

            Assert.AreEqual(1, lookup.Questions.Count);
            StringAssert.Contains(lookup.Questions[0], AllowedTarget, StringComparison.Ordinal);
            StringAssert.Contains(lookup.Questions[0], "FUNCTION_BLOCK", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Propose_LookupIsBroken_ChangeStillRuns()
        {
            // The rule this stage must not break. A machine without Node, or with a broken index,
            // still writes; it simply writes without citations, and the plan says why.
            var outcome = GuardWith(new BrokenHardwareLookup()).Propose(Request(AllowedTarget), () => "written", Now);

            Assert.IsTrue(outcome.IsApplied, "a failed lookup must not stop a write");
            Assert.AreEqual(HardwareContextOutcome.Unavailable, outcome.Plan!.Documentation.Outcome);
            StringAssert.Contains(outcome.Plan.Documentation.Reason, "node is not installed", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Propose_PolicyRefuses_NothingIsLookedUp()
        {
            // A change that will not happen does not need illustrating, and the plan says the
            // lookup never ran rather than showing an empty result that looks like a silence.
            var lookup = new StubHardwareLookup(HardwareContext.Cited(new[] { Citation() }));

            var outcome = GuardWith(lookup).Propose(Request(ForbiddenTarget), () => "written", Now);

            Assert.IsFalse(outcome.IsApplied);
            Assert.AreEqual(0, lookup.Questions.Count, "a refused change must not be looked up");
            Assert.AreEqual(ChangeRequest.NotLookedUp, outcome.Plan!.Documentation.Reason);
        }

        [TestMethod]
        public void Request_BuiltOutsideTheGuard_SaysItWasNeverLookedUp()
        {
            // A plan made without the guard is visibly uncited rather than quietly blank, so a path
            // that skipped the lookup cannot pass for one that found nothing.
            var plan = new ChangePlan(PlanId.Create(), Request(AllowedTarget), OperationMode.Study, Now);

            Assert.AreEqual(HardwareContextOutcome.Unavailable, plan.Documentation.Outcome);
            Assert.AreEqual(ChangeRequest.NotLookedUp, plan.Documentation.Reason);
        }

        [TestMethod]
        public void ToString_CitedPlan_ShowsTheSourceBesideTheChange()
        {
            var request = Request(AllowedTarget).WithDocumentation(HardwareContext.Cited(new[] { Citation() }));

            var description = new ChangePlan(PlanId.Create(), request, OperationMode.Study, Now).ToString();

            StringAssert.Contains(description, AllowedTarget, StringComparison.Ordinal);
            StringAssert.Contains(description, "page 47", StringComparison.Ordinal);
        }

        [TestMethod]
        public void WithDocumentation_Null_Refuses()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Request(AllowedTarget).WithDocumentation(null!));
        }

        [TestMethod]
        public void WithDocumentation_AfterABackup_KeepsTheBackupPath()
        {
            // The two things the system attaches to a request are independent, and attaching one
            // must not drop the other — a lost backup path is a write with no way back.
            var request = Request(AllowedTarget).WithBackup(@"C:\backups\FB_Station.xml");

            var cited = request.WithDocumentation(HardwareContext.NotFound());

            Assert.AreEqual(@"C:\backups\FB_Station.xml", cited.BackupPath);
        }

        private static GuardedWrite GuardWith(IHardwareLookup lookup)
        {
            var policy = new WritePolicy(new Dictionary<OperationMode, ModeRules>
            {
                [OperationMode.Study] = new ModeRules(
                    OperationMode.Study,
                    new[] { "PLC_0/Blocks/*" },
                    Array.Empty<string>())
            });

            return new GuardedWrite(
                new StubModeGate(OperationMode.Study),
                policy,
                new RecordingAuditTrail(),
                new ChangePlanStore(new FixedClock(Now)),
                lookup);
        }

        private static ChangeRequest Request(string target)
        {
            return new ChangeRequest("WriteScl", target, "FUNCTION_BLOCK FB_Station", "test");
        }

        private static HardwareCitation Citation()
        {
            var document = new SourceDocument("UR5e", "Universal Robots e-Series User Manual UR5e", "SW 5.16");

            return new HardwareCitation(document, 47, "configurable I/O can be set as safety-related");
        }
    }
}
