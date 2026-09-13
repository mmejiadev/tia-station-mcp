using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Subnets: the wires themselves, read and created, as opposed to the interfaces plugged into
    /// them that PortalNetwork covers. It shares that file's private helpers -- the device lookup
    /// that takes a backup first, the node lookup that names the nodes that do exist, and the
    /// single decoration point -- because a subnet write is a network write in every respect.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads every subnet in the open project.</summary>
        /// <returns>One entry per subnet, with its type, its members and its IO systems.</returns>
        /// <exception cref="PortalException">No project is open.</exception>
        /// <remarks>
        /// The read that has to come before connecting anything: a subnet is referred to by a name
        /// somebody chose, and there is no way to guess it. It also answers the question the
        /// topology cannot, which is what an interface could be attached to.
        /// </remarks>
        public IReadOnlyList<SubnetInfo> GetSubnets()
        {
            _logger?.LogInformation("Reading subnets...");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading the subnets");
                }

                return new SubnetReader(_logger).Read(AllSubnets());
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the subnets failed: {ex.Message}", null, ex);

                _logger?.LogError(pex, "GetSubnets failed");
                throw pex;
            }
        }

        /// <summary>Creates a subnet and connects one interface to it.</summary>
        /// <param name="deviceItemPath">Path of the device item owning the interface.</param>
        /// <param name="nodeName">The node, as GetNetworkTopology names it.</param>
        /// <param name="subnetName">The name to give the subnet.</param>
        /// <param name="backupDirectory">Where the current layout is recorded first. Required.</param>
        /// <returns>The subnet the node sits on afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the device item or the node does not exist, the name is taken, the
        /// node is already on a subnet, or TIA Portal refused.
        /// </exception>
        /// <remarks>
        /// Aimed with the same two columns as SetNodeAddress, for the same reason: a write whose
        /// arguments cannot be obtained from a read is a write nobody can aim.
        /// </remarks>
        public string CreateSubnet(string deviceItemPath, string nodeName, string subnetName, string backupDirectory)
        {
            _logger?.LogInformation("Creating subnet {Subnet} from {Node} on {Device}...", subnetName, nodeName, deviceItemPath);

            try
            {
                var node = RequireNodeForWrite(deviceItemPath, nodeName, backupDirectory);

                return new SubnetConfigurator(_logger).Create(node, AllSubnets(), subnetName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, deviceItemPath, backupDirectory, "CreateSubnet");
            }
        }

        /// <summary>Connects one interface to a subnet that already exists.</summary>
        /// <param name="deviceItemPath">Path of the device item owning the interface.</param>
        /// <param name="nodeName">The node, as GetNetworkTopology names it.</param>
        /// <param name="subnetName">The subnet to attach it to, as GetSubnets names it.</param>
        /// <param name="backupDirectory">Where the current layout is recorded first. Required.</param>
        /// <returns>The subnet the node sits on afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the device item, the node or the subnet does not exist, the node is
        /// on a different subnet, the network types do not match, or TIA Portal refused.
        /// </exception>
        public string ConnectDeviceToSubnet(string deviceItemPath, string nodeName, string subnetName, string backupDirectory)
        {
            _logger?.LogInformation("Connecting {Node} on {Device} to subnet {Subnet}...", nodeName, deviceItemPath, subnetName);

            try
            {
                var node = RequireNodeForWrite(deviceItemPath, nodeName, backupDirectory);

                return new SubnetConfigurator(_logger).Connect(node, AllSubnets(), subnetName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, deviceItemPath, backupDirectory, "ConnectDeviceToSubnet");
            }
        }

        /// <summary>
        /// Every subnet an interface can be on, which is what both the read and the writes use.
        /// </summary>
        /// <remarks>
        /// One enumeration for the tool that lists subnets and the tools that connect to them. The
        /// project's own composition is not that set -- see <see cref="SubnetLookup"/> -- and using
        /// it directly is how GetSubnets came to deny a subnet the topology was printing.
        /// </remarks>
        private IReadOnlyList<Subnet> AllSubnets()
        {
            return new SubnetLookup().All(_project!.Subnets, FindDevices());
        }

        /// <summary>
        /// The node a network write is aimed at, with the layout recorded before anything changes.
        /// </summary>
        private Node RequireNodeForWrite(string deviceItemPath, string nodeName, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "nodeName is required");
            }

            var deviceItem = RequireDeviceItemForWrite(deviceItemPath, backupDirectory);

            return RequireNode(deviceItem, deviceItemPath, nodeName);
        }
    }
}
