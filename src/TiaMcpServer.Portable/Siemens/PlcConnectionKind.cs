namespace TiaMcpServer.Siemens
{
    /// <summary>The connections between two PLCs this server makes.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21, between a CPU 1511-1 PN V4.0 and a CPU 1215C V4.7 on one subnet: each made
    /// through <c>CommunicationManagement.Connections.Create&lt;T&gt;</c> came out valid and both stations compiled. Openness
    /// offers FDL, ISO, PtP and UDP connections too; they are left out until somebody measures them.
    /// </remarks>
    public enum PlcConnectionKind
    {
        /// <summary>An S7 connection, the one PUT, GET, BSEND and BRCV use.</summary>
        S7,

        /// <summary>A TCP connection, for TSEND and TRCV.</summary>
        Tcp,

        /// <summary>An ISO-on-TCP connection (RFC 1006), for TSEND and TRCV with TSAPs.</summary>
        IsoOnTcp
    }
}
