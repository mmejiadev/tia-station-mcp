using System;
using System.Text.Json.Nodes;
using TiaMcpServer.Governance;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// How a write tool asks the governance layer for permission, and what it answers when the
    /// answer is no.
    /// </summary>
    /// <remarks>
    /// Every tool that changes the project, a virtual controller or the project on disk goes
    /// through here, so the sequence — policy, plan, audit, execute — is written down once instead
    /// of sixteen times drifting apart.
    ///
    /// **A refusal is a response, not an exception.** The caller is told what was refused and why,
    /// in the shape it would have received had the change run, with <c>success</c> false and an
    /// empty payload. An expected refusal thrown as an exception would reach the caller as an
    /// operation failure, which invites a retry of something that must not be retried.
    ///
    /// **A change awaiting confirmation loses its typed payload**, and that is inherent rather than
    /// an oversight: the work runs later, from <c>ApplyChange</c>, which reports it as text. It
    /// costs nothing in Study Mode, where whitelisted changes confirm themselves and the caller
    /// gets the full response; in Workshop Mode the caller is meant to look at the plan, not at a
    /// payload that does not exist yet.
    /// </remarks>
    public static class GuardedTool
    {
        /// <summary>
        /// Metadata key naming what the guard decided, present only when the change did **not** run.
        /// </summary>
        /// <remarks>
        /// A constant because two places depend on it and they must not drift: this class writes it,
        /// and the job wrapper reads it to tell "the tool ran" from "the guard stopped it". When that
        /// was a literal in both, a job could report success for a change the guard had refused.
        /// </remarks>
        public const string OutcomeKey = "outcome";

        /// <summary>Runs a write through the guard, or reports why it did not run.</summary>
        /// <typeparam name="TResponse">The response the tool returns when the change runs.</typeparam>
        /// <param name="guard">The session's single write path.</param>
        /// <param name="request">What is being asked for, and on what.</param>
        /// <param name="execute">The work itself. It runs only if the guard says so.</param>
        /// <param name="empty">Builds the payload-free response used when nothing was written.</param>
        /// <returns>The tool's own response when applied; an empty one carrying the reason otherwise.</returns>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <exception cref="PortalException">The work ran but returned nothing.</exception>
        public static TResponse Run<TResponse>(
            GuardedWrite guard,
            ChangeRequest request,
            Func<TResponse> execute,
            Func<TResponse> empty)
            where TResponse : ResponseMessage
        {
            if (guard == null)
            {
                throw new ArgumentNullException(nameof(guard));
            }

            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            if (empty == null)
            {
                throw new ArgumentNullException(nameof(empty));
            }

            TResponse? applied = null;

            var outcome = guard.Propose(
                request,
                () =>
                {
                    applied = execute();

                    return applied?.Message ?? string.Empty;
                },
                DateTimeOffset.UtcNow);

            if (!outcome.IsApplied)
            {
                return Describe(empty(), outcome);
            }

            if (applied == null)
            {
                // The audit trail already says this change was applied, so returning a refusal
                // shape here would contradict the record. It is a defect in the tool, not a policy
                // decision, and it is reported as one.
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{request?.Tool}' reported success without producing a response.");
            }

            return applied;
        }

        private static TResponse Describe<TResponse>(TResponse response, ChangeOutcome outcome)
            where TResponse : ResponseMessage
        {
            response.Message = outcome.Detail;
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = false,
                [OutcomeKey] = outcome.Kind.ToString(),
                ["planId"] = outcome.Plan != null ? outcome.Plan.Id.Value : string.Empty
            };

            return response;
        }
    }
}
