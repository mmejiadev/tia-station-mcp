using System;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What happened to a proposed change.
    /// </summary>
    /// <remarks>
    /// A result rather than an exception, because being refused by the policy is the system
    /// working, not a failure. Exceptions here are reserved for what nobody planned for — TIA
    /// Portal dying mid-operation, the audit trail unwritable.
    ///
    /// The distinction is not stylistic: an expected refusal arriving as an exception gets caught
    /// by the portal layer's decoration point and reported as an operation failure, which tells
    /// the caller to retry something it must not retry.
    /// </remarks>
    public sealed class ChangeOutcome
    {
        private ChangeOutcome(ChangeOutcomeKind kind, ChangePlan? plan, string detail, string result)
        {
            Kind = kind;
            Plan = plan;
            Detail = detail;
            Result = result;
        }

        /// <summary>Whether the change ran, is waiting for a person, or was refused.</summary>
        public ChangeOutcomeKind Kind { get; }

        /// <summary>The plan, when one was made. Null when the request never became one.</summary>
        public ChangePlan? Plan { get; }

        /// <summary>Why, in terms a caller can act on.</summary>
        public string Detail { get; }

        /// <summary>What the change returned, when it ran.</summary>
        public string Result { get; }

        /// <summary>Whether the change actually ran.</summary>
        public bool IsApplied => Kind == ChangeOutcomeKind.Applied;

        /// <summary>The change ran.</summary>
        /// <param name="plan">The plan that ran.</param>
        /// <param name="result">What it returned.</param>
        /// <param name="detail">Anything worth saying about how it went.</param>
        /// <returns>The outcome.</returns>
        public static ChangeOutcome Applied(ChangePlan plan, string result, string detail = "")
        {
            return new ChangeOutcome(ChangeOutcomeKind.Applied, plan, detail, result);
        }

        /// <summary>The change is planned and waiting for a person to confirm it.</summary>
        /// <param name="plan">The plan awaiting confirmation.</param>
        /// <returns>The outcome.</returns>
        public static ChangeOutcome AwaitingConfirmation(ChangePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            return new ChangeOutcome(
                ChangeOutcomeKind.AwaitingConfirmation,
                plan,
                $"Confirm with ApplyChange('{plan.Id}') before {plan.Expiry:u}. Nothing has been written yet.",
                string.Empty);
        }

        /// <summary>The change was refused and nothing was written.</summary>
        /// <param name="reason">Why.</param>
        /// <param name="plan">The plan, when one had been made.</param>
        /// <returns>The outcome.</returns>
        public static ChangeOutcome Refused(string reason, ChangePlan? plan = null)
        {
            return new ChangeOutcome(ChangeOutcomeKind.Refused, plan, reason, string.Empty);
        }
    }
}
