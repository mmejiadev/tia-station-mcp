namespace TiaMcpServer.Jobs
{
    /// <summary>
    /// Where one long operation has got to.
    /// </summary>
    /// <remarks>
    /// Five states and no sixth, so a switch over them can be exhaustive. There is deliberately no
    /// "cancelling": Openness cannot be interrupted once it has started, so a job either never
    /// started or runs to its end.
    /// </remarks>
    public enum JobState
    {
        /// <summary>Accepted, not started. The only state a job can be cancelled from.</summary>
        Queued,

        /// <summary>Inside Openness. It will finish or fail; nothing can stop it.</summary>
        Running,

        /// <summary>Finished, with a result to read.</summary>
        Succeeded,

        /// <summary>Finished by throwing, with the reason to read.</summary>
        Failed,

        /// <summary>Cancelled before it started, so nothing ran.</summary>
        Cancelled
    }
}
