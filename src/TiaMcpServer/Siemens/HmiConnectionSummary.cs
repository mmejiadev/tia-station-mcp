namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// An integrated HMI connection as TIA Portal made it: its name and the addresses at either end.
    /// </summary>
    public sealed class HmiConnectionSummary
    {
        /// <summary>Creates the description of a connection.</summary>
        /// <param name="name">The name TIA Portal gave it, e.g. <c>HMI_Connection_1</c>.</param>
        /// <param name="localAddress">The HMI station's address.</param>
        /// <param name="partnerAddress">The PLC's address.</param>
        public HmiConnectionSummary(string name, string localAddress, string partnerAddress)
        {
            Name = name;
            LocalAddress = localAddress;
            PartnerAddress = partnerAddress;
        }

        /// <summary>The name TIA Portal gave it.</summary>
        public string Name { get; }

        /// <summary>The HMI station's address.</summary>
        public string LocalAddress { get; }

        /// <summary>The PLC's address.</summary>
        public string PartnerAddress { get; }
    }
}
