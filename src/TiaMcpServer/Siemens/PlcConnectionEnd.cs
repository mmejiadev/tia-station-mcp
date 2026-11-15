namespace TiaMcpServer.Siemens
{
    /// <summary>One end of a PLC connection: whose it is, where it answers, and how the program addresses it.</summary>
    public sealed class PlcConnectionEnd
    {
        /// <summary>Creates the end.</summary>
        /// <param name="device">The device item at this end, e.g. <c>PLC_1</c>; empty when TIA Portal names none.</param>
        /// <param name="address">Its address on the connection's subnet.</param>
        /// <param name="connectionId">The ID the program's instructions use for this connection at this end, e.g. <c>100</c>.</param>
        /// <param name="addressing">How the protocol tells the connection apart, e.g. <c>TSAP SIMATIC-ACC10001</c> or <c>port 2000</c>.</param>
        public PlcConnectionEnd(string device, string address, string connectionId, string addressing)
        {
            Device = device;
            Address = address;
            ConnectionId = connectionId;
            Addressing = addressing;
        }

        /// <summary>The device item at this end.</summary>
        public string Device { get; }

        /// <summary>Its address on the connection's subnet.</summary>
        public string Address { get; }

        /// <summary>The ID the program's instructions use for this connection at this end.</summary>
        public string ConnectionId { get; }

        /// <summary>How the protocol tells the connection apart: a TSAP or a port.</summary>
        public string Addressing { get; }
    }
}
