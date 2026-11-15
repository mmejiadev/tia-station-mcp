namespace TiaMcpServer.Siemens
{
    /// <summary>What a connection between two PLCs is asked to be: who establishes it, to whom, and of what kind.</summary>
    internal sealed class PlcConnectionRequest
    {
        /// <summary>Creates the request.</summary>
        /// <param name="plcPath">The PLC that establishes it.</param>
        /// <param name="partnerPlcPath">The PLC at the other end.</param>
        /// <param name="kind">Its kind.</param>
        internal PlcConnectionRequest(string plcPath, string partnerPlcPath, PlcConnectionKind kind)
        {
            PlcPath = plcPath;
            PartnerPlcPath = partnerPlcPath;
            Kind = kind;
        }

        /// <summary>The PLC that establishes it.</summary>
        internal string PlcPath { get; }

        /// <summary>The PLC at the other end.</summary>
        internal string PartnerPlcPath { get; }

        /// <summary>Its kind.</summary>
        internal PlcConnectionKind Kind { get; }
    }
}
