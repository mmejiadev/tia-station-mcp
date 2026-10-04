using System;
using TiaMcpServer.Knowledge;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// The single point every write passes through.
    /// </summary>
    /// <remarks>
    /// **One execution path.** There is no branch that writes without producing and recording a
    /// plan first — not even in Study Mode, where the plan confirms itself. A "skip the checks"
    /// path would exist in the Workshop build too, and an untested branch is the one that
    /// eventually runs with a machine connected. What differs between the modes is only who
    /// confirms.
    ///
    /// The sequence never varies: check the policy, make a plan, record it, then either run it or
    /// hold it for a person. Recording happens **before** the work, so a change that kills the
    /// process mid-write still left a line saying it was about to happen.
    /// </remarks>
    public sealed class GuardedWrite
    {
        private static readonly TimeSpan PlanLifetime = TimeSpan.FromMinutes(10);

        private readonly IModeGate _gate;
        private readonly IWritePolicy _policy;
        private readonly IAuditTrail _audit;
        private readonly ChangePlanStore _plans;
        private readonly IHardwareLookup _documentation;
        private readonly IProjectContext _project;

        /// <summary>Creates the guard.</summary>
        /// <param name="gate">What this session may act on.</param>
        /// <param name="policy">Which targets it may write to.</param>
        /// <param name="audit">Where every decision is written down.</param>
        /// <param name="plans">Where plans wait for confirmation.</param>
        /// <param name="documentation">What the manuals say about the equipment being changed.</param>
        /// <param name="project">Which project is open, recorded with every change.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <remarks>
        /// Six collaborators against the repository's limit of four parameters, and a parameter
        /// object is deliberately not used here. The limit exists so that a long positional list
        /// cannot be passed in the wrong order — which, for a guard, would mean auditing through
        /// the wrong trail. These six are distinct types, five of them interfaces, so a wrong order
        /// does not compile, and wrapping them would move the same six arguments one level out while
        /// adding a class that means nothing on its own. Stated rather than assumed, per the closing
        /// note of CLAUDE.md.
        /// </remarks>
        public GuardedWrite(
            IModeGate gate,
            IWritePolicy policy,
            IAuditTrail audit,
            ChangePlanStore plans,
            IHardwareLookup documentation,
            IProjectContext project)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
            _documentation = documentation ?? throw new ArgumentNullException(nameof(documentation));
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        /// <summary>Proposes a change, and runs it when this mode confirms automatically.</summary>
        /// <param name="request">What is being asked for.</param>
        /// <param name="execute">What running it does.</param>
        /// <param name="now">The current moment, in UTC.</param>
        /// <returns>Whether it ran, is waiting for a person, or was refused.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="execute"/> is null.</exception>
        public ChangeOutcome Propose(ChangeRequest request, Func<string> execute, DateTimeOffset now)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            // Before the decision, so that a refusal records the project it was refused in too.
            request = request.WithProject(_project.CurrentProjectPath);

            var decision = _policy.Decide(_gate.Mode, request.Target);

            if (!decision.IsAllowed)
            {
                return Refuse(request, decision, now);
            }

            var plan = new ChangePlan(PlanId.Create(), Cite(request), _gate.Mode, now + PlanLifetime);
            var unrecorded = Record(plan, AuditOutcome.Planned, decision.Reason, now);

            if (_gate.RequiredConfirmation == Confirmation.Manual)
            {
                // No unrecorded plan can reach here: manual confirmation means Workshop Mode, and
                // there Record does not catch at all - a trail it cannot write refuses the work.
                _plans.Add(plan, execute);

                return ChangeOutcome.AwaitingConfirmation(plan);
            }

            return Run(plan, execute, now, unrecorded);
        }

        /// <summary>Records a refusal and reports it.</summary>
        /// <param name="request">What was asked for.</param>
        /// <param name="decision">Why the policy said no.</param>
        /// <param name="now">The current moment, in UTC.</param>
        /// <returns>The refusal, carrying the plan that was written down.</returns>
        /// <remarks>
        /// A refused change is not looked up, and its plan says so rather than showing citations. A
        /// change that will not happen does not need illustrating, and asking the index about one
        /// would spend a process on explaining something the guard has already stopped.
        /// </remarks>
        private ChangeOutcome Refuse(ChangeRequest request, PolicyDecision decision, DateTimeOffset now)
        {
            var plan = new ChangePlan(PlanId.Create(), request, _gate.Mode, now + PlanLifetime);
            var unrecorded = Record(plan, AuditOutcome.Refused, decision.Reason, now);

            return ChangeOutcome.Refused(Combine(decision.Reason, unrecorded), plan);
        }

        /// <summary>Attaches what the documentation says about the change.</summary>
        /// <param name="request">The change about to be planned.</param>
        /// <returns>The same request, carrying citations or an honest silence.</returns>
        /// <remarks>
        /// Here rather than in each tool, because this is the one place every write passes through:
        /// a citation attached at sixteen call sites is a citation fifteen of them eventually forget.
        /// The question is the change's own words — what is being written, and to what — and the
        /// index abstains when they cover nothing it holds, which is why a vague change produces a
        /// not-found instead of an irrelevant excerpt.
        /// </remarks>
        private ChangeRequest Cite(ChangeRequest request)
        {
            return request.WithDocumentation(_documentation.Describe($"{request.Target} {request.Value}"));
        }

        /// <summary>Runs a plan a person has confirmed.</summary>
        /// <param name="id">Which plan.</param>
        /// <param name="now">The current moment, in UTC.</param>
        /// <returns>Whether it ran or was refused.</returns>
        /// <exception cref="PortalException">No such plan, or it has expired.</exception>
        public ChangeOutcome Confirm(PlanId id, DateTimeOffset now)
        {
            var pending = _plans.Take(id);
            var mismatch = MismatchWithSession(pending.Plan);

            if (mismatch.Length > 0)
            {
                var unrecorded = Record(pending.Plan, AuditOutcome.Refused, mismatch, now);

                return ChangeOutcome.Refused(Combine(mismatch, unrecorded), pending.Plan);
            }

            return Run(pending.Plan, pending.Execute, now, string.Empty);
        }

        /// <summary>Why a plan no longer describes what confirming it would do, or empty when it still does.</summary>
        /// <param name="plan">The plan a person is confirming.</param>
        /// <returns>The reason, or an empty string.</returns>
        /// <remarks>
        /// Both are the same failure: a plan approved for one thing that would do another. Made in
        /// one mode and confirmed in another, or proposed with one project open and confirmed with
        /// another — where the work would land in the second project while the trail, under its
        /// hash, says the first. Paths are compared without regard to case, as Windows compares them.
        /// </remarks>
        private string MismatchWithSession(ChangePlan plan)
        {
            if (plan.Mode != _gate.Mode)
            {
                return $"Plan '{plan.Id}' was made in {plan.Mode} mode and this session is in {_gate.Mode}.";
            }

            var open = _project.CurrentProjectPath;

            if (!string.Equals(plan.Project, open, StringComparison.OrdinalIgnoreCase))
            {
                return $"Plan '{plan.Id}' was made with project '{Describe(plan.Project)}' open, and the project " +
                       $"open now is '{Describe(open)}'. Open the project it was made for, or propose it again.";
            }

            return string.Empty;
        }

        private static string Describe(string projectPath)
        {
            return projectPath.Length == 0 ? "none" : projectPath;
        }

        /// <summary>Runs a plan and records how it went.</summary>
        /// <param name="plan">The plan to run.</param>
        /// <param name="execute">What running it does.</param>
        /// <param name="now">The current moment, in UTC.</param>
        /// <param name="unrecorded">Anything earlier that could not be written down.</param>
        /// <returns>The outcome, carrying every audit failure that reached it.</returns>
        private ChangeOutcome Run(ChangePlan plan, Func<string> execute, DateTimeOffset now, string unrecorded)
        {
            try
            {
                var result = execute();
                var trouble = Combine(unrecorded, Record(plan, AuditOutcome.Applied, string.Empty, now));

                return ChangeOutcome.Applied(plan, result, trouble);
            }
            catch (Exception exception)
            {
                // Recorded before rethrowing: a change that failed halfway is exactly the one
                // somebody will need to find later, and it is the one least likely to be
                // remembered.
                var trouble = Combine(unrecorded, Record(plan, AuditOutcome.Failed, exception.Message, now));

                if (trouble.Length > 0)
                {
                    // The change failed and the failure could not be written down either. No
                    // outcome is returned on this path, so the only place a caller is guaranteed to
                    // look is the exception itself.
                    exception.Data["auditFailure"] = trouble;
                }

                throw;
            }
        }

        /// <summary>Joins a reason with an audit failure, when there is one.</summary>
        /// <param name="reason">What the caller was going to be told.</param>
        /// <param name="unrecorded">The audit failure, or an empty string.</param>
        /// <returns>Both, or just the reason.</returns>
        private static string Combine(string reason, string unrecorded)
        {
            if (unrecorded.Length == 0)
            {
                return reason;
            }

            return reason.Length == 0 ? unrecorded : reason + " " + unrecorded;
        }

        /// <summary>
        /// Writes an audit entry, refusing the whole operation when that is not possible in
        /// Workshop Mode.
        /// </summary>
        /// <remarks>
        /// The fail-closed inversion. Acting on a machine without leaving a trace is worse than
        /// not acting, so in Workshop Mode an unwritable audit trail stops the work. In Study Mode
        /// the same failure is reported and the work continues: the worst case there is a
        /// simulation nobody can reconstruct.
        /// </remarks>
        /// <param name="plan">What was decided.</param>
        /// <param name="outcome">How it ended.</param>
        /// <param name="detail">Why, when there is a why.</param>
        /// <param name="now">The current moment, in UTC.</param>
        /// <returns>Empty when the entry was written; why it was not, when it was not.</returns>
        /// <exception cref="PortalException">The trail could not be written, in Workshop Mode.</exception>
        private string Record(ChangePlan plan, AuditOutcome outcome, string detail, DateTimeOffset now)
        {
            try
            {
                _audit.Append(new AuditEntry(now, plan, outcome, detail));

                return string.Empty;
            }
            catch (PortalException failure) when (_gate.Mode == OperationMode.Study)
            {
                // Handed back, never swallowed. Until the audit of 2026-09-02 this block was empty
                // and its comment claimed the failure was reported through the outcome. Nothing
                // reported it, so a Study run could lose entries with nobody told - and the
                // workshop gate reads that trail to decide whether a machine may be switched on.
                // Every caller now puts this into the outcome it returns.
                return "The audit trail could not be written, so this change is not in it: " + failure.Message;
            }
        }
    }
}
