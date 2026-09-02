namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// The lookup used when this machine has no documentation index configured.
    /// </summary>
    /// <remarks>
    /// A class rather than a null reference, so every plan carries a hardware context and no caller
    /// has to remember to check whether it has one. This stage adds a field to the plan; it does not
    /// add a way for that field to be absent.
    ///
    /// It is also the default. Nothing is looked up unless a machine has been pointed at an index,
    /// and a machine that has not been says so in every plan rather than staying quiet about it.
    /// </remarks>
    public sealed class UnavailableHardwareLookup : IHardwareLookup
    {
        /// <summary>The reason reported in every plan on a machine with no index.</summary>
        public const string Reason = "no documentation index is configured for this server";

        /// <summary>Answers that no lookup is possible here.</summary>
        /// <param name="question">Ignored; there is nothing to ask.</param>
        /// <returns>An unavailable context naming the missing configuration.</returns>
        public HardwareContext Describe(string question)
        {
            return HardwareContext.Unavailable(Reason);
        }
    }
}
