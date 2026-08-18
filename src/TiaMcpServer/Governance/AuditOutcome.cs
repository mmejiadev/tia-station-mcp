namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What became of an audited operation.
    /// </summary>
    /// <remarks>
    /// Every entry carries one. "No silent failures" — one of the five conditions for opening
    /// Workshop Mode — is the claim that no entry ever ends in an unknown state, and that claim is
    /// only checkable because the outcome is written down rather than inferred.
    /// </remarks>
    public enum AuditOutcome
    {
        /// <summary>A change was planned. It has not run, and may never run.</summary>
        Planned = 0,

        /// <summary>The change ran and succeeded.</summary>
        Applied = 1,

        /// <summary>The change was refused before running: policy, mode, or an expired plan.</summary>
        Refused = 2,

        /// <summary>The change ran and failed. Distinct from refused: something happened.</summary>
        Failed = 3
    }
}
