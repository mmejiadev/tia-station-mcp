namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A device that answered a search for accessible devices, as TIA Portal's "Accessible devices" lists it.
    /// </summary>
    public sealed class AccessibleDeviceInfo
    {
        /// <summary>Creates the description of a device that answered.</summary>
        /// <param name="name">Its name, e.g. <c>S7-1500 CPU:192.168.0.1</c>.</param>
        /// <param name="address">Its address.</param>
        /// <param name="macAddress">Its MAC address.</param>
        /// <param name="deviceSeries">Its family, e.g. <c>S7-1500 CPU</c>.</param>
        public AccessibleDeviceInfo(string name, string address, string macAddress, string deviceSeries)
        {
            Name = name;
            Address = address;
            MacAddress = macAddress;
            DeviceSeries = deviceSeries;
        }

        /// <summary>Its name.</summary>
        public string Name { get; }

        /// <summary>Its address.</summary>
        public string Address { get; }

        /// <summary>Its MAC address.</summary>
        public string MacAddress { get; }

        /// <summary>Its family.</summary>
        public string DeviceSeries { get; }
    }
}
