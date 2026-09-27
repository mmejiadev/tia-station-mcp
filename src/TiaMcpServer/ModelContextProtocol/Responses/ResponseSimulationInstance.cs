using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One PLCSIM Advanced virtual controller.</summary>
    public sealed class ResponseSimulationInstance : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="name">The instance name.</param>
        /// <param name="operatingState">Off, Stop, Run and so on.</param>
        /// <param name="cpuType">The CPU being emulated.</param>
        /// <param name="ipAddresses">Addresses the controller answers on.</param>
        public ResponseSimulationInstance(
            string name,
            string operatingState,
            string cpuType,
            IReadOnlyList<string> ipAddresses)
        {
            Name = name;
            OperatingState = operatingState;
            CpuType = cpuType;
            IpAddresses = ipAddresses;
        }

        /// <summary>The instance name.</summary>
        public string Name { get; }

        /// <summary>
        /// Off, Stop, Run and so on. A controller with no program cannot reach Run: download first.
        /// </summary>
        public string OperatingState { get; }

        /// <summary>The CPU being emulated.</summary>
        public string CpuType { get; }

        /// <summary>
        /// Addresses the controller answers on. A new instance reports 0.0.0.0 until an address is
        /// set, and TIA Portal cannot download to it in that state.
        /// </summary>
        public IReadOnlyList<string> IpAddresses { get; }
    }
}
