namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Whether the policy allows a target, and why.
    /// </summary>
    /// <remarks>
    /// The reason travels with the decision because a refusal a caller cannot explain is a
    /// refusal a caller will work around. "Denied by rule <c>PLC_0/*</c>" is actionable;
    /// "not allowed" invites guessing.
    /// </remarks>
    public sealed class PolicyDecision
    {
        private PolicyDecision(bool isAllowed, string reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        /// <summary>Whether the change may proceed.</summary>
        public bool IsAllowed { get; }

        /// <summary>Why, in terms a caller can act on.</summary>
        public string Reason { get; }

        /// <summary>The target is on the allow list.</summary>
        /// <param name="rule">The rule that allowed it.</param>
        /// <returns>An allowing decision.</returns>
        public static PolicyDecision Allow(string rule)
        {
            return new PolicyDecision(true, $"allowed by rule '{rule}'");
        }

        /// <summary>The target is refused.</summary>
        /// <param name="reason">Why.</param>
        /// <returns>A refusing decision.</returns>
        public static PolicyDecision Refuse(string reason)
        {
            return new PolicyDecision(false, reason);
        }
    }
}
