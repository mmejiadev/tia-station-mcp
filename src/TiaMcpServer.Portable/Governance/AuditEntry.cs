using System;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// One line of the audit trail: something that already happened.
    /// </summary>
    /// <remarks>
    /// Immutable, because it describes the past. Nothing here has a setter and every field is
    /// fixed by the constructor.
    ///
    /// The fields are the questions someone asks three sessions later when something is broken:
    /// when, in which mode, which tool, on what, with what value, how it ended, under which plan,
    /// and where the backup went.
    /// </remarks>
    public sealed class AuditEntry
    {
        /// <summary>Creates an audit entry.</summary>
        /// <param name="timestamp">When it happened, in UTC.</param>
        /// <param name="plan">The change this entry belongs to.</param>
        /// <param name="outcome">How it ended.</param>
        /// <param name="detail">Why it ended that way, when that is not obvious.</param>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
        public AuditEntry(DateTimeOffset timestamp, ChangePlan plan, AuditOutcome outcome, string detail = "")
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            Timestamp = timestamp;
            PlanId = plan.Id;
            Mode = plan.Mode;
            Tool = plan.Tool;
            Target = plan.Target;
            Value = plan.Value;
            BackupPath = plan.BackupPath;
            Origin = plan.Origin;
            Outcome = outcome;
            Detail = detail ?? string.Empty;
        }

        /// <summary>When it happened, in UTC.</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>The plan this entry belongs to.</summary>
        public PlanId PlanId { get; }

        /// <summary>Which mode the session was in.</summary>
        public OperationMode Mode { get; }

        /// <summary>Which tool asked for the change.</summary>
        public string Tool { get; }

        /// <summary>What it was going to touch.</summary>
        public string Target { get; }

        /// <summary>What it was going to write, summarised.</summary>
        public string Value { get; }

        /// <summary>Where the previous state was saved, or empty when nothing was overwritten.</summary>
        public string BackupPath { get; }

        /// <summary>Who or what asked: a user, an agent, a command.</summary>
        public string Origin { get; }

        /// <summary>How it ended.</summary>
        public AuditOutcome Outcome { get; }

        /// <summary>Why it ended that way, when that is not obvious from the outcome alone.</summary>
        public string Detail { get; }
    }
}
