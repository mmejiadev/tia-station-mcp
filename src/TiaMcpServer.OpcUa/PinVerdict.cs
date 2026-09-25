namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// What the pin file said about the certificate a server presented.
    /// </summary>
    public enum PinVerdict
    {
        /// <summary>No certificate was on file for this server; this one was recorded.</summary>
        FirstUse,

        /// <summary>The certificate is the one recorded earlier.</summary>
        Matches,

        /// <summary>
        /// A different certificate was recorded earlier. The connection is refused.
        /// </summary>
        Changed
    }
}
