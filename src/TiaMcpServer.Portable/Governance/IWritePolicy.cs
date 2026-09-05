namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Decides which targets a session may write to.
    /// </summary>
    /// <remarks>
    /// Deny by default, in both modes. A target that matches nothing is refused, because the
    /// alternative — allowing what nobody thought about — is how a whitelist stops being one.
    /// </remarks>
    public interface IWritePolicy
    {
        /// <summary>Decides whether a target may be written to.</summary>
        /// <param name="mode">The mode the session is in.</param>
        /// <param name="target">The full path being written to.</param>
        /// <returns>The decision and its reason.</returns>
        public PolicyDecision Decide(OperationMode mode, string target);
    }
}
