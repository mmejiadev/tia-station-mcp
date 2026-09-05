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
    /// where the backup went, and what documentation the change was justified with.
    /// </remarks>
    public sealed class AuditEntry
    {
        /// <summary>Creates an audit entry.</summary>
        /// <param name="timestamp">When it happened, in UTC.</param>
        /// <param name="plan">The change this entry belongs to.</param>
        /// <param name="outcome">How it ended.</param>
        /// <param name="detail">Why it ended that way, when that is not obvious.</param>
        /// <param name="documentation">
        /// What the documentation index could cite, when the entry is being read back from a trail
        /// rather than recorded. Null means take it from the plan, which is what recording does.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
        /// <remarks>
        /// The last parameter exists because a plan reconstructed from a line of the trail cannot
        /// carry the citation that line records: the summary is lossy on purpose, so there is no way
        /// back from it to a <c>HardwareContext</c>. Without it, an entry written with a citation
        /// read back saying there was none, and the trail would misreport itself to the gate that
        /// reads it.
        /// </remarks>
        public AuditEntry(
            DateTimeOffset timestamp,
            ChangePlan plan,
            AuditOutcome outcome,
            string detail = "",
            string? documentation = null)
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
            Documentation = documentation ?? plan.Documentation.Summarise();
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

        /// <summary>What the documentation index could cite for this change, as the plan showed it.</summary>
        /// <remarks>
        /// The plan has always carried this and the trail never recorded it, which was audit finding
        /// F3: the citation was shown to whoever confirmed the change and then lost, so the trail the
        /// workshop gate reads could not say what a change had been justified with.
        ///
        /// The summary rather than the excerpts. It names document, version and page, which is what
        /// makes a claim checkable; carrying the excerpt itself would put a paragraph of a third
        /// party's manual on every line of the trail without making it any more verifiable.
        ///
        /// "Nothing was found" and "there is no index on this machine" are recorded as plainly as a
        /// citation is. A change made with no documentation behind it is a fact about that change.
        /// </remarks>
        public string Documentation { get; }
    }
}
