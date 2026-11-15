namespace TiaMcpServer.Siemens
{
    /// <summary>The two ends of a PLC connection, and which of them establishes it.</summary>
    public sealed class PlcConnectionEnds
    {
        /// <summary>Creates the pair.</summary>
        /// <param name="local">The end of the PLC the connection was read from.</param>
        /// <param name="partner">The other end.</param>
        /// <param name="isLocalActive">Whether the local end establishes the connection; the other end then waits for it.</param>
        public PlcConnectionEnds(PlcConnectionEnd local, PlcConnectionEnd partner, bool isLocalActive)
        {
            Local = local;
            Partner = partner;
            IsLocalActive = isLocalActive;
        }

        /// <summary>The end of the PLC the connection was read from.</summary>
        public PlcConnectionEnd Local { get; }

        /// <summary>The other end.</summary>
        public PlcConnectionEnd Partner { get; }

        /// <summary>Whether the local end establishes the connection.</summary>
        public bool IsLocalActive { get; }
    }
}
