namespace TiaMcpServer.Governance
{
    /// <summary>What became of a proposed change.</summary>
    public enum ChangeOutcomeKind
    {
        /// <summary>Nothing was written, and the reason says why.</summary>
        Refused = 0,

        /// <summary>Nothing has been written yet: a person has to confirm the plan first.</summary>
        AwaitingConfirmation = 1,

        /// <summary>The change ran.</summary>
        Applied = 2
    }
}
