using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What one mode may write to.
    /// </summary>
    /// <remarks>
    /// Deny wins over allow, and anything matching neither is refused. Both halves matter: deny
    /// beating allow lets a broad rule be narrowed without rewriting it, and refusing the
    /// unmatched is what makes this a whitelist rather than a suggestion.
    /// </remarks>
    public sealed class ModeRules
    {
        private readonly IReadOnlyList<TargetPattern> _allow;
        private readonly IReadOnlyList<TargetPattern> _deny;

        /// <summary>Creates the rules for one mode.</summary>
        /// <param name="mode">The mode these rules govern.</param>
        /// <param name="allow">Patterns that may be written to.</param>
        /// <param name="deny">Patterns that may not, whatever the allow list says.</param>
        /// <exception cref="PortalException">
        /// A Workshop rule contains a wildcard. See <see cref="RequireNoWildcards"/>.
        /// </exception>
        public ModeRules(OperationMode mode, IEnumerable<string> allow, IEnumerable<string> deny)
        {
            Mode = mode;
            _allow = (allow ?? Array.Empty<string>()).Select(TargetPattern.Parse).ToList();
            _deny = (deny ?? Array.Empty<string>()).Select(TargetPattern.Parse).ToList();

            if (mode == OperationMode.Workshop)
            {
                RequireNoWildcards();
            }
        }

        /// <summary>The mode these rules govern.</summary>
        public OperationMode Mode { get; }

        /// <summary>Decides whether a target may be written to.</summary>
        /// <param name="target">The full path being written to.</param>
        /// <returns>The decision and its reason.</returns>
        public PolicyDecision Decide(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return PolicyDecision.Refuse("no target was named");
            }

            var denied = _deny.FirstOrDefault(pattern => pattern.Matches(target));

            if (denied != null)
            {
                return PolicyDecision.Refuse($"denied by rule '{denied.Pattern}' for {Mode} mode");
            }

            var allowed = _allow.FirstOrDefault(pattern => pattern.Matches(target));

            if (allowed != null)
            {
                return PolicyDecision.Allow(allowed.Pattern);
            }

            return PolicyDecision.Refuse(
                $"'{target}' is on no allow list for {Mode} mode. Nothing is permitted unless it is listed.");
        }

        /// <summary>
        /// Refuses wildcards, which Workshop rules may not contain.
        /// </summary>
        /// <remarks>
        /// A wildcard is a rule about targets nobody enumerated. That is a reasonable trade in
        /// Study Mode, where the worst case is a broken simulation, and not one to make about a
        /// machine that can move. Enforced when the policy loads rather than when it is consulted,
        /// so a policy that could not be honoured is refused before any work is done against it.
        /// </remarks>
        /// <exception cref="PortalException">Any rule contains a wildcard.</exception>
        private void RequireNoWildcards()
        {
            var offending = _allow.Concat(_deny).Where(pattern => pattern.HasWildcard).ToList();

            if (offending.Count == 0)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                "Workshop rules may not contain wildcards; every target must be written out in full. " +
                $"Offending: {string.Join(", ", offending.Select(pattern => $"'{pattern.Pattern}'"))}");
        }
    }
}
