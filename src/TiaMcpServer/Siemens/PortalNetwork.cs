using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// PROFINET: the topology as it stands, and the IO system that binds devices to a controller.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>What Openness calls a node's address. The reader uses the same name.</summary>
        private const string NodeAddressAttribute = "Address";

        /// <summary>
        /// Reads the project's network layout: every device interface, its address and its subnet.
        /// </summary>
        /// <returns>One entry per interface, whether or not it is attached to a subnet.</returns>
        /// <exception cref="PortalException">No project is open.</exception>
        public IReadOnlyList<NetworkNodeInfo> GetNetworkTopology()
        {
            _logger?.LogInformation("Reading network topology...");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading the network topology");
                }

                return new NetworkTopologyReader(_logger).Read(FindDevices());
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the network topology failed: {ex.Message}", null, ex);

                _logger?.LogError(pex, "GetNetworkTopology failed");
                throw pex;
            }
        }

        /// <summary>
        /// Adds the network layout to a program snapshot.
        /// </summary>
        /// <remarks>
        /// A snapshot that records the program but not the network it runs on describes half a
        /// project: the same blocks addressing a device at a different address are a different
        /// system, and nothing in the export would show it.
        /// </remarks>
        private SnapshotResult WithNetworkTopology(SnapshotResult program, string targetDirectory)
        {
            var exported = new List<string>(program.Exported)
            {
                NetworkTopologyWriter.Write(targetDirectory, new NetworkTopologyReader(_logger).Read(FindDevices()))
            };

            return new SnapshotResult(exported, program.Inconsistent, program.Unsupported, program.Failed);
        }

        /// <summary>
        /// Creates a PROFINET IO system on a CPU, so IO devices can be attached to it.
        /// </summary>
        /// <param name="controllerPath">Full path to the CPU, for example <c>PLC_0</c>.</param>
        /// <param name="ioSystemName">Name for the IO system.</param>
        /// <param name="backupDirectory">
        /// Where the network layout is recorded before the change. Required: this rewires the
        /// project, and the repository rule is that every write is preceded by an export.
        /// </param>
        /// <returns>The subnet the IO system was created on.</returns>
        /// <exception cref="PortalException">
        /// The arguments are invalid, no project is open, the path does not resolve, or the device
        /// cannot act as an IO controller.
        /// </exception>
        public string CreateIoSystem(string controllerPath, string ioSystemName, string backupDirectory)
        {
            _logger?.LogInformation("Creating IO system {IoSystem} on {Controller}...", ioSystemName, controllerPath);

            try
            {
                var controller = RequireDeviceItemForWrite(controllerPath, backupDirectory);

                return new NetworkConfigurator(_logger).CreateIoSystem(controller, ioSystemName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, controllerPath, backupDirectory, "CreateIoSystem");
            }
        }

        /// <summary>Attaches a device to an existing PROFINET IO system.</summary>
        /// <param name="devicePath">Full path to the IO device.</param>
        /// <param name="ioSystemName">The IO system to attach it to.</param>
        /// <param name="backupDirectory">Where the network layout is recorded before the change.</param>
        /// <exception cref="PortalException">
        /// The arguments are invalid, no project is open, the path does not resolve, the device has
        /// no IO connector, or the IO system is unknown.
        /// </exception>
        public void AssignDeviceToIoSystem(string devicePath, string ioSystemName, string backupDirectory)
        {
            _logger?.LogInformation("Attaching {Device} to IO system {IoSystem}...", devicePath, ioSystemName);

            try
            {
                var device = RequireDeviceItemForWrite(devicePath, backupDirectory);

                new NetworkConfigurator(_logger).AssignToIoSystem(device, ioSystemName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, devicePath, backupDirectory, "AssignDeviceToIoSystem");
            }
        }

        /// <summary>Sets the address of one network node.</summary>
        /// <param name="deviceItemPath">Path of the device item owning the interface.</param>
        /// <param name="nodeName">The node, as GetNetworkTopology names it.</param>
        /// <param name="address">The address to set.</param>
        /// <param name="backupDirectory">Where the current layout is recorded first. Required.</param>
        /// <returns>The address the node holds afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the device item or the node does not exist, or TIA Portal refused the
        /// address.
        /// </exception>
        /// <remarks>
        /// **Its two names are two columns of GetNetworkTopology.** That read tool already prints
        /// device path and node name for every interface in the project, so finding what to change
        /// and changing it use the same vocabulary. A write whose arguments cannot be obtained from
        /// a read is a write nobody can aim.
        ///
        /// The address is read back afterwards instead of being echoed, for the reason
        /// WriteSimulationTag gives: reporting what was asked for tells the caller nothing about
        /// what happened. TIA normalises some addresses, and a caller that trusted the echo would
        /// go on to download to an address the project does not have.
        ///
        /// This is why the tool exists at all: a download to PLCSIM only connects when the CPU
        /// address in the project matches the virtual controller's, and until now that address
        /// could only be typed into TIA Portal by hand.
        /// </remarks>
        public string SetNodeAddress(string deviceItemPath, string nodeName, string address, string backupDirectory)
        {
            _logger?.LogInformation("Setting {Node} on {Device} to {Address}...", nodeName, deviceItemPath, address);

            try
            {
                if (string.IsNullOrWhiteSpace(nodeName) || string.IsNullOrWhiteSpace(address))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "nodeName and address are required");
                }

                var deviceItem = RequireDeviceItemForWrite(deviceItemPath, backupDirectory);
                var node = RequireNode(deviceItem, deviceItemPath, nodeName);

                SetAddress(node, address);

                return node.GetAttribute(NodeAddressAttribute)?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, deviceItemPath, backupDirectory, "SetNodeAddress");
            }
        }

        /// <remarks>
        /// Names the nodes that do exist when the wanted one does not. The alternative message,
        /// "node not found", sends somebody back to TIA Portal to look up a name this server could
        /// have printed.
        /// </remarks>
        private static Node RequireNode(DeviceItem deviceItem, string deviceItemPath, string nodeName)
        {
            var networkInterface = deviceItem.GetService<NetworkInterface>()
                ?? throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{deviceItemPath}' has no network interface, so it has no address to set");

            foreach (Node node in networkInterface.Nodes)
            {
                if (string.Equals(node.Name, nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            var present = string.Join(", ", networkInterface.Nodes.Select(one => one.Name));

            throw new PortalException(
                PortalErrorCode.NotFound,
                $"'{deviceItemPath}' has no node called '{nodeName}'. It has: {present}");
        }

        /// <remarks>
        /// A refused address is the caller's mistake, so it comes back as invalid input rather than
        /// as an operation failure. The message carries what the node holds now, because the shape
        /// differs by network: an Ethernet node takes 192.168.0.1 and a PROFIBUS node takes a
        /// station number, and guessing which this is would be worse than showing the current one.
        /// </remarks>
        private static void SetAddress(Node node, string address)
        {
            var before = node.GetAttribute(NodeAddressAttribute)?.ToString() ?? string.Empty;

            try
            {
                node.SetAttribute(NodeAddressAttribute, address);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"TIA Portal refused '{address}' for node '{node.Name}'. It holds '{before}' now, " +
                    "which is the shape this network expects.",
                    null,
                    failure);
            }
        }

        private DeviceItem RequireDeviceItemForWrite(string devicePath, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this rewires the project");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before changing the network");
            }

            var deviceItem = FindDeviceItem(devicePath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {devicePath}");

            NetworkTopologyWriter.Write(backupDirectory, new NetworkTopologyReader(_logger).Read(FindDevices()));

            return deviceItem;
        }

        private PortalException DecorateNetworkFailure(Exception ex, string devicePath, string backupDirectory, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["devicePath"] = devicePath;
            pex.Data["backupDirectory"] = backupDirectory;

            _logger?.LogError(pex, "{Operation} failed for {DevicePath}", operation, devicePath);

            return pex;
        }
    }
}
