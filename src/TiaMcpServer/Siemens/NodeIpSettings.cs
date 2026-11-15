namespace TiaMcpServer.Siemens
{
    /// <summary>The IP settings of a node besides its address: its subnet mask and the router it uses, if any.</summary>
    public sealed class NodeIpSettings
    {
        /// <summary>Creates the settings.</summary>
        /// <param name="subnetMask">The subnet mask, e.g. <c>255.255.255.0</c>.</param>
        /// <param name="routerAddress">The router, e.g. <c>192.168.0.254</c>; empty for none.</param>
        public NodeIpSettings(string subnetMask, string routerAddress)
        {
            SubnetMask = subnetMask;
            RouterAddress = routerAddress;
        }

        /// <summary>The subnet mask.</summary>
        public string SubnetMask { get; }

        /// <summary>The router; empty when the node uses none.</summary>
        public string RouterAddress { get; }

        /// <summary>Whether the node uses a router.</summary>
        public bool UsesRouter => !string.IsNullOrWhiteSpace(RouterAddress);
    }
}
