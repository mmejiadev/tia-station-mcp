using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The PROFINET device name: the name an IO controller resolves over DCP before it talks IP.
    /// It sits apart from the rest of the network because it is the one network property that is
    /// not about wires — a device can be on the right subnet, at the right address, and still never
    /// join its IO system because the name does not match the hardware.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Sets the PROFINET device name of one network node.</summary>
        /// <param name="deviceItemPath">Path of the device item owning the interface.</param>
        /// <param name="nodeName">The node, as GetNetworkTopology names it.</param>
        /// <param name="deviceName">The PROFINET device name to set.</param>
        /// <param name="backupDirectory">Where the current layout is recorded first. Required.</param>
        /// <returns>The name the node holds afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the device item or the node does not exist, the node is not a
        /// PROFINET node, or TIA Portal refused the name.
        /// </exception>
        /// <remarks>
        /// Aimed with the two columns GetNetworkTopology prints, like SetDeviceAddress, and the
        /// name it ends up with is read back for the same reason: TIA normalises what it stores,
        /// and a caller that trusted the echo would go on to configure hardware with a name the
        /// project does not hold.
        /// </remarks>
        public string SetProfinetDeviceName(string deviceItemPath, string nodeName, string deviceName, string backupDirectory)
        {
            _logger?.LogInformation("Naming {Node} on {Device} as {DeviceName} on PROFINET...", nodeName, deviceItemPath, deviceName);

            try
            {
                var node = RequireNodeForWrite(deviceItemPath, nodeName, backupDirectory);

                return new ProfinetDeviceNaming(_logger).Set(node, deviceName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, deviceItemPath, backupDirectory, "SetProfinetDeviceName");
            }
        }
    }
}
