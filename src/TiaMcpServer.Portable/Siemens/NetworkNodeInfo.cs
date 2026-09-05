namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One network attachment point: a device's interface, its address, and the subnet it is on.
    /// </summary>
    /// <remarks>
    /// The unit an agent needs in order to reason about a distributed cell. Code that addresses
    /// remote IO is unwriteable without knowing which devices exist, how they are connected and at
    /// what addresses — and a snapshot that versions the program but not the network describes
    /// half a project.
    /// </remarks>
    public sealed class NetworkNodeInfo
    {
        /// <summary>Creates a network node description.</summary>
        /// <param name="devicePath">Full path of the device item owning the interface.</param>
        /// <param name="interfaceName">Name of the interface, for example <c>PROFINET interface_1</c>.</param>
        /// <param name="networkType">Ethernet, Profibus and so on.</param>
        /// <param name="address">The node's address on its subnet.</param>
        /// <param name="subnetName">The subnet it is attached to, or empty when unconnected.</param>
        public NetworkNodeInfo(
            string devicePath,
            string interfaceName,
            string networkType,
            string address,
            string subnetName)
        {
            DevicePath = devicePath;
            InterfaceName = interfaceName;
            NetworkType = networkType;
            Address = address;
            SubnetName = subnetName;
        }

        /// <summary>Full path of the device item owning the interface.</summary>
        public string DevicePath { get; }

        /// <summary>Name of the interface.</summary>
        public string InterfaceName { get; }

        /// <summary>Ethernet, Profibus and so on.</summary>
        public string NetworkType { get; }

        /// <summary>The node's address on its subnet.</summary>
        public string Address { get; }

        /// <summary>
        /// The subnet it is attached to. Empty means the interface exists but is not wired to
        /// anything, which is a common reason a download or an IO connection fails.
        /// </summary>
        public string SubnetName { get; }

        /// <summary>True when the interface is attached to a subnet.</summary>
        public bool IsConnected => !string.IsNullOrEmpty(SubnetName);
    }
}
