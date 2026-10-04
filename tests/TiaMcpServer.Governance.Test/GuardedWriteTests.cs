using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Knowledge;
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
        private const string OpenProject = @"C:\Projects\Cell\Cell.ap20";

        /// <remarks>
        /// The target names a path inside a project and not the project, so this is the only place
        /// the trail learns which project a change was made in. Both lines carry it: a process that
        /// dies after planning still left a record of where it was going to write.
        /// </remarks>
        [TestMethod]
        public void Propose_InStudy_RecordsTheOpenProjectOnEveryLine()
        {
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Study, audit);

            guard.Propose(Request(AllowedTarget), () => "written", Now);

            CollectionAssert.AreEqual(
                new[] { OpenProject, OpenProject },
                audit.Entries.Select(entry => entry.Project).ToArray());
        }

        /// <remarks>
        /// Workshop Mode: proposed with one project open, confirmed after another was opened. The work
        /// would land in the second while the trail, under its hash, says the first — so it is refused,
        /// as a plan confirmed in another mode is, and the refusal is recorded.
        /// </remarks>
        [TestMethod]
        public void Confirm_WithAnotherProjectOpen_IsRefusedAndRunsNothing()
        {
            var project = new FixedProjectContext(OpenProject);
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Workshop, audit, project);
            var ran = false;

            var proposed = guard.Propose(Request(AllowedTarget), () => { ran = true; return "written"; }, Now);
            project.CurrentProjectPath = @"C:\Projects\Other\Other.ap20";
            var confirmed = guard.Confirm(proposed.Plan!.Id, Now);

            Assert.AreEqual(ChangeOutcomeKind.Refused, confirmed.Kind);
            Assert.IsFalse(ran, "a plan confirmed in another project must not run");
            Assert.AreEqual(AuditOutcome.Refused, audit.Entries[audit.Entries.Count - 1].Outcome);
        }

        [TestMethod]
        public void Confirm_TheSameProjectInOtherCase_RunsThePlan()
        {
            var project = new FixedProjectContext(OpenProject);
            var guard = GuardFor(OperationMode.Workshop, new RecordingAuditTrail(), project);

            var proposed = guard.Propose(Request(AllowedTarget), () => "written", Now);
            project.CurrentProjectPath = OpenProject.ToUpperInvariant();
            var confirmed = guard.Confirm(proposed.Plan!.Id, Now);

            Assert.AreEqual(ChangeOutcomeKind.Applied, confirmed.Kind);
        }

        [TestMethod]
        public void Propose_ARefusedChange_RecordsTheProjectItWasRefusedIn()
        {
            var audit = new RecordingAuditTrail();
            var guard = GuardFor(OperationMode.Study, audit);

            guard.Propose(Request(ForbiddenTarget), () => "written", Now);

            Assert.AreEqual(OpenProject, audit.Entries.Single().Project);
        }

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
        public void Propose_InStudy_WhenTheAuditTrailCannotBeWritten_SaysSoInTheOutcome()
        {
            // CLAUDE.md: in Study Mode a failed audit write proceeds *and reports*. Until the audit
            // of 2026-09-02 only the first half was true - the catch block was empty and its
            // comment claimed a report that nothing produced, so a run could lose entries with
            // nobody told. The test above passes either way, which is exactly why this one exists.
            var guard = GuardFor(OperationMode.Study, new UnwritableAuditTrail());

            var outcome = guard.Propose(Request(AllowedTarget), () => "written", Now);

            StringAssert.Contains(
                outcome.Detail,
                "audit trail could not be written",
                "an audit failure nobody is told about is a silent failure",
                StringComparison.Ordinal);
        }

        [TestMethod]
        public void Propose_InStudy_WhenTheTrailIsWritable_ReportsNothingExtra()
        {
            // The other side of it: a working trail must not decorate every successful write with
            // a warning, or the warning stops meaning anything.
            var guard = GuardFor(OperationMode.Study, new RecordingAuditTrail());

            var outcome = guard.Propose(Request(AllowedTarget), () => "written", Now);

            Assert.AreEqual(string.Empty, outcome.Detail);
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

        private static GuardedWrite GuardFor(OperationMode mode, IAuditTrail audit, FixedProjectContext project)
        {
            return GuardFor(mode, audit, new GuardDependencies(null, null, project));
        }

        private static GuardedWrite GuardFor(
            OperationMode mode,
            IAuditTrail audit,
            FixedClock? clock = null,
            IHardwareLookup? lookup = null)
        {
            return GuardFor(mode, audit, new GuardDependencies(clock, lookup, null));
        }

        /// <summary>The collaborators a test may replace; null keeps the default.</summary>
        private sealed class GuardDependencies
        {
            public GuardDependencies(FixedClock? clock, IHardwareLookup? lookup, FixedProjectContext? project)
            {
                Clock = clock;
                Lookup = lookup;
                Project = project;
            }

            public FixedClock? Clock { get; }

            public IHardwareLookup? Lookup { get; }

            public FixedProjectContext? Project { get; }
        }

        private static GuardedWrite GuardFor(OperationMode mode, IAuditTrail audit, GuardDependencies dependencies)
        {
            var policy = new WritePolicy(new Dictionary<OperationMode, ModeRules>
            {
                [OperationMode.Study] = new ModeRules(OperationMode.Study, new[] { "PLC_0/Blocks/*" }, Array.Empty<string>()),
                [OperationMode.Workshop] = new ModeRules(OperationMode.Workshop, new[] { AllowedTarget }, Array.Empty<string>())
            });

            return new GuardedWrite(
                new StubModeGate(mode),
                policy,
                audit,
                new ChangePlanStore(dependencies.Clock ?? new FixedClock(Now)),
                dependencies.Lookup ?? new UnavailableHardwareLookup(),
                dependencies.Project ?? new FixedProjectContext(OpenProject));
        }
    }
}
