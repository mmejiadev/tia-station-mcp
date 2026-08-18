using System;
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

        /// <summary>Creates the guard.</summary>
        /// <param name="gate">What this session may act on.</param>
        /// <param name="policy">Which targets it may write to.</param>
        /// <param name="audit">Where every decision is written down.</param>
        /// <param name="plans">Where plans wait for confirmation.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public GuardedWrite(IModeGate gate, IWritePolicy policy, IAuditTrail audit, ChangePlanStore plans)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
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

            var decision = _policy.Decide(_gate.Mode, request.Target);
            var plan = new ChangePlan(PlanId.Create(), request, _gate.Mode, now + PlanLifetime);

            if (!decision.IsAllowed)
            {
                Record(plan, AuditOutcome.Refused, decision.Reason, now);

                return ChangeOutcome.Refused(decision.Reason, plan);
            }

            Record(plan, AuditOutcome.Planned, decision.Reason, now);

            if (_gate.RequiredConfirmation == Confirmation.Manual)
            {
                _plans.Add(plan, execute);

                return ChangeOutcome.AwaitingConfirmation(plan);
            }

            return Run(plan, execute, now);
        }

        /// <summary>Runs a plan a person has confirmed.</summary>
        /// <param name="id">Which plan.</param>
        /// <param name="now">The current moment, in UTC.</param>
        /// <returns>Whether it ran or was refused.</returns>
        /// <exception cref="PortalException">No such plan, or it has expired.</exception>
        public ChangeOutcome Confirm(PlanId id, DateTimeOffset now)
        {
            var pending = _plans.Take(id);

            if (pending.Plan.Mode != _gate.Mode)
            {
                // A plan made in one mode confirmed in another describes work nobody approved.
                var reason = $"Plan '{id}' was made in {pending.Plan.Mode} mode and this session is in {_gate.Mode}.";

                Record(pending.Plan, AuditOutcome.Refused, reason, now);

                return ChangeOutcome.Refused(reason, pending.Plan);
            }

            return Run(pending.Plan, pending.Execute, now);
        }

        private ChangeOutcome Run(ChangePlan plan, Func<string> execute, DateTimeOffset now)
        {
            try
            {
                var result = execute();

                Record(plan, AuditOutcome.Applied, string.Empty, now);

                return ChangeOutcome.Applied(plan, result);
            }
            catch (Exception exception)
            {
                // Recorded before rethrowing: a change that failed halfway is exactly the one
                // somebody will need to find later, and it is the one least likely to be
                // remembered.
                Record(plan, AuditOutcome.Failed, exception.Message, now);

                throw;
            }
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
        /// <exception cref="PortalException">The trail could not be written, in Workshop Mode.</exception>
        private void Record(ChangePlan plan, AuditOutcome outcome, string detail, DateTimeOffset now)
        {
            try
            {
                _audit.Append(new AuditEntry(now, plan, outcome, detail));
            }
            catch (PortalException) when (_gate.Mode == OperationMode.Study)
            {
                // Reported through the outcome rather than swallowed: see ChangeOutcome.Detail.
            }
        }
    }
}
