using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// One test per rule <see cref="GuardedWrite"/> is supposed to enforce. These are the rules
    /// that stand between an agent and a machine, so none of them may rest on incidental coverage.
    /// </remarks>
    [TestClass]
    public sealed class GuardedWriteTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero);
        private const string AllowedTarget = "PLC_0/Blocks/FB_Station";
        private const string ForbiddenTarget = "PLC_0/Safety/FB_Estop";

        [TestMethod]
        public void Propose_InStudy_RunsAndRecordsBothPlanAndOutcome()
        {
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Study, audit);
            var ran = false;

            var outcome = guard.Propose(Request(AllowedTarget), () => { ran = true; return "written"; }, Now);

            Assert.AreEqual(ChangeOutcomeKind.Applied, outcome.Kind);
            Assert.AreEqual("written", outcome.Result);
            Assert.IsTrue(ran);

            // Planned before the work, Applied after: a process that dies mid-write still left the
            // line saying what it was about to do.
            CollectionAssert.AreEqual(
                new[] { AuditOutcome.Planned, AuditOutcome.Applied },
                audit.Entries.Select(entry => entry.Outcome).ToArray());
        }

        [TestMethod]
        public void Propose_InWorkshop_WaitsForAPersonAndWritesNothing()
        {
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Workshop, audit);
            var ran = false;

            var outcome = guard.Propose(Request(AllowedTarget), () => { ran = true; return "written"; }, Now);

            Assert.AreEqual(ChangeOutcomeKind.AwaitingConfirmation, outcome.Kind);
            Assert.IsFalse(ran, "Workshop Mode must not run anything before a person confirms it");
            StringAssert.Contains(outcome.Detail, "Nothing has been written yet", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Confirm_AfterProposing_RunsExactlyTheWorkThatWasDescribed()
        {
            var guard = GuardFor(OperationMode.Workshop, new RecordingAuditTrail());
            var runs = 0;

            var proposed = guard.Propose(Request(AllowedTarget), () => { runs++; return "written"; }, Now);
            var applied = guard.Confirm(proposed.Plan!.Id, Now);

            Assert.AreEqual(ChangeOutcomeKind.Applied, applied.Kind);
            Assert.AreEqual(1, runs);
        }

        [TestMethod]
        public void Confirm_Twice_IsRefused()
        {
            // A confirmation is spent when it is used. Replaying one would let a single approval
            // authorise a second write nobody looked at.
            var guard = GuardFor(OperationMode.Workshop, new RecordingAuditTrail());
            var proposed = guard.Propose(Request(AllowedTarget), () => "written", Now);

            guard.Confirm(proposed.Plan!.Id, Now);

            Assert.ThrowsException<PortalException>(() => guard.Confirm(proposed.Plan.Id, Now));
        }

        [TestMethod]
        public void Confirm_AfterExpiry_IsRefused()
        {
            // An old confirmation may no longer describe what would happen.
            var clock = new FixedClock(Now);
            var guard = GuardFor(OperationMode.Workshop, new RecordingAuditTrail(), clock);
            var proposed = guard.Propose(Request(AllowedTarget), () => "written", Now);

            clock.Advance(TimeSpan.FromHours(1));

            var exception = Assert.ThrowsException<PortalException>(() => guard.Confirm(proposed.Plan!.Id, clock.UtcNow));

            StringAssert.Contains(exception.Message, "expired", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Propose_TargetOffTheWhitelist_NeverRuns()
        {
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Study, audit);
            var ran = false;

            var outcome = guard.Propose(Request(ForbiddenTarget), () => { ran = true; return "written"; }, Now);

            Assert.AreEqual(ChangeOutcomeKind.Refused, outcome.Kind);
            Assert.IsFalse(ran);

            // Refusals are recorded too. A whitelist nobody can see working is one nobody trusts.
            Assert.AreEqual(AuditOutcome.Refused, audit.Entries.Single().Outcome);
        }

        [TestMethod]
        public void Propose_InWorkshop_WhenTheAuditTrailCannotBeWritten_RefusesTheWork()
        {
            // The fail-closed inversion, and the reason the audit trail is not merely a log:
            // acting on a machine without leaving a trace is worse than not acting.
            var guard = GuardFor(OperationMode.Workshop, new UnwritableAuditTrail());
            var ran = false;

            Assert.ThrowsException<PortalException>(
                () => guard.Propose(Request(AllowedTarget), () => { ran = true; return "written"; }, Now));

            Assert.IsFalse(ran, "Workshop Mode must not act when it cannot record that it acted");
        }

        [TestMethod]
        public void Propose_InStudy_WhenTheAuditTrailCannotBeWritten_StillRuns()
        {
            // The other half of the same rule. The worst case here is a simulation nobody can
            // reconstruct, which is not worth stopping the work over.
            var guard = GuardFor(OperationMode.Study, new UnwritableAuditTrail());

            var outcome = guard.Propose(Request(AllowedTarget), () => "written", Now);

            Assert.AreEqual(ChangeOutcomeKind.Applied, outcome.Kind);
        }

        [TestMethod]
        public void Run_WhenTheWorkThrows_RecordsTheFailureBeforeRethrowing()
        {
            // The change that failed halfway is the one somebody will need to find later, and the
            // one least likely to be remembered.
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Study, audit);

            Assert.ThrowsException<InvalidOperationException>(
                () => guard.Propose(Request(AllowedTarget), () => throw new InvalidOperationException("TIA died"), Now));

            Assert.AreEqual(AuditOutcome.Failed, audit.Entries[audit.Entries.Count - 1].Outcome);
            StringAssert.Contains(audit.Entries[audit.Entries.Count - 1].Detail, "TIA died", StringComparison.Ordinal);
        }

        private static ChangeRequest Request(string target)
        {
            return new ChangeRequest("WriteScl", target, "FUNCTION_BLOCK ...", "test");
        }

        private static GuardedWrite GuardFor(OperationMode mode, IAuditTrail audit, FixedClock? clock = null)
        {
            var policy = new WritePolicy(new Dictionary<OperationMode, ModeRules>
            {
                [OperationMode.Study] = new ModeRules(OperationMode.Study, new[] { "PLC_0/Blocks/*" }, Array.Empty<string>()),
                [OperationMode.Workshop] = new ModeRules(OperationMode.Workshop, new[] { AllowedTarget }, Array.Empty<string>())
            });

            return new GuardedWrite(new StubModeGate(mode), policy, audit, new ChangePlanStore(clock ?? new FixedClock(Now)));
        }
    }
}
