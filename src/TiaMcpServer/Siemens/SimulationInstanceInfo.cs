using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A PLCSIM Advanced virtual controller, as data rather than as a runtime handle.
    /// </summary>
    /// <remarks>
    /// The runtime hands out <c>IInstance</c> objects that hold a live connection to a virtual PLC.
    /// Those never cross out of this layer: a caller that holds one keeps the instance alive.
    /// </remarks>
    public sealed class SimulationInstanceInfo
    {
        /// <summary>Creates an instance description.</summary>
        /// <param name="name">The instance name, unique within the runtime.</param>
        /// <param name="operatingState">Whether the virtual PLC is off, stopped, running…</param>
        /// <param name="cpuType">The CPU the instance emulates.</param>
        /// <param name="ipAddresses">The addresses the controller answers on.</param>
        /// <param name="licenseStatus">Whether the controller holds a PLCSIM Advanced licence.</param>
        public SimulationInstanceInfo(
            string name,
            string operatingState,
            string cpuType,
            IReadOnlyList<string> ipAddresses,
            string licenseStatus)
        {
            Name = name;
            OperatingState = operatingState;
            CpuType = cpuType;
            IpAddresses = ipAddresses;
            LicenseStatus = licenseStatus;
        }

        /// <summary>The instance name, unique within the runtime.</summary>
        public string Name { get; }

        /// <summary>
        /// Whether the virtual PLC is off, stopped or running. A freshly registered instance is
        /// powered off, which is not the same as stopped: a download needs it powered on first.
        /// </summary>
        public string OperatingState { get; }

        /// <summary>The CPU the instance emulates.</summary>
        public string CpuType { get; }

        /// <summary>
        /// The addresses the controller answers on. Empty in softbus mode, where the instance is
        /// reachable through the PLCSIM bus rather than over IP — which is exactly the difference
        /// that decides whether a download over the virtual Ethernet adapter can connect.
        /// </summary>
        public IReadOnlyList<string> IpAddresses { get; }

        /// <summary>
        /// Whether the controller holds a PLCSIM Advanced licence.
        /// </summary>
        /// <remarks>
        /// The runtime reports <c>LicenseNotFound</c> and <c>NoLicenseAvailable</c> as error codes
        /// of their own, and an unlicensed controller can be created and powered on while still
        /// refusing to do useful work. Surfacing it here means "why will it not download" can be
        /// answered without guessing at the licence.
        /// </remarks>
        public string LicenseStatus { get; }
    }
}
