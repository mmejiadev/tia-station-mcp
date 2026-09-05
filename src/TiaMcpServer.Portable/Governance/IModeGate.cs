namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What a session may act on, as everything downstream needs to know it.
    /// </summary>
    /// <remarks>
    /// An interface so that <see cref="GuardedWrite"/> depends on the question rather than on the
    /// class that answers it. The practical consequence is that Workshop behaviour — one manual
    /// confirmation per action, an audit failure refusing the work — can be tested in the ordinary
    /// build, where <see cref="ModeGate.ForWorkshop"/> is compiled out and could never produce a
    /// gate to test with.
    ///
    /// That does not weaken layer 0. What is compiled out is the ability to *reach physical
    /// hardware*; a test double claiming Workshop Mode only gets the stricter rules applied to it,
    /// which is exactly what needs checking. In the server, only <see cref="ModeGate"/> is
    /// registered.
    /// </remarks>
    public interface IModeGate
    {
        /// <summary>What this session may act on.</summary>
        public OperationMode Mode { get; }

        /// <summary>Who confirms a planned change in this session.</summary>
        public Confirmation RequiredConfirmation { get; }
    }
}
