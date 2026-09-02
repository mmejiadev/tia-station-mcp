namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// Asks the documentation index what it can cite about a change.
    /// </summary>
    /// <remarks>
    /// An interface so the governance layer depends on the question rather than on who answers it.
    /// The index itself lives in the harness, in TypeScript, and the implementation that reaches it
    /// is one class; a machine without it gets <see cref="UnavailableHardwareLookup"/> and a plan
    /// that says so.
    ///
    /// **It never throws for a failed lookup.** A missing index, an absent runtime or a malformed
    /// answer are all reported as <see cref="HardwareContextOutcome.Unavailable"/> carrying the
    /// reason, because a citation is context attached to a change, never a condition of making one.
    /// A lookup that could refuse a write would be a new way to stop the guard, which is the
    /// opposite of what this stage adds.
    /// </remarks>
    public interface IHardwareLookup
    {
        /// <summary>Looks up what the documentation says about a question.</summary>
        /// <param name="question">What to ask, in the words of the change being planned.</param>
        /// <returns>Excerpts, an honest not-found, or an unavailable carrying the reason.</returns>
        HardwareContext Describe(string question);
    }
}
