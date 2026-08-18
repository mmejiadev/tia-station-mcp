namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Who confirms a planned change before it is executed.
    /// </summary>
    /// <remarks>
    /// This is the *only* thing that differs between the two modes. Every write produces a plan
    /// and is recorded either way, because a branch that executes without a plan would exist in
    /// the Workshop build too, and an untested branch is the one that eventually runs with a
    /// machine connected.
    /// </remarks>
    public enum Confirmation
    {
        /// <summary>
        /// The plan confirms itself, provided the policy allows the target.
        /// </summary>
        /// <remarks>
        /// Not "no confirmation": the plan is still built and still audited. What is skipped is
        /// waiting for a person, which on a simulated controller buys nothing.
        /// </remarks>
        Automatic = 0,

        /// <summary>
        /// A person confirms, one action at a time, before anything is executed.
        /// </summary>
        Manual = 1
    }
}
