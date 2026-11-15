using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// A node's subnet mask and router, the two IP settings besides the address that SetNodeAddress sets.
    ///
    /// Measured 2026-11-15 on TIA Portal V21, on the X1 node of a CPU 1511-1 PN V4.0: <c>SubnetMask</c>,
    /// <c>UseRouter</c> and <c>RouterAddress</c> are writable attributes of the node and read back as written. Everything
    /// is checked before anything is recorded or written (<see cref="IpSettingsRule"/>): TIA Portal stores a router
    /// outside the node's subnet without complaint, and the PLC then never reaches it. The node's own attributes are
    /// recorded first, as the network record holds addresses but not masks or routers; and as the three are written one
    /// by one, a refusal part-way puts back the ones already changed.
    /// </remarks>
    public partial class Portal
    {
        private const string SubnetMaskAttribute = "SubnetMask";
        private const string UseRouterAttribute = "UseRouter";
        private const string RouterAddressAttribute = "RouterAddress";

        /// <summary>Sets a node's subnet mask and router.</summary>
        /// <param name="deviceItemPath">Path of the device item owning the interface, as GetNetworkTopology prints it.</param>
        /// <param name="nodeName">The node, as GetNetworkTopology prints it.</param>
        /// <param name="settings">The mask, and the router or empty for none.</param>
        /// <param name="backupDirectory">Where the node and the network are recorded first. Required.</param>
        /// <returns>The settings the node holds afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the node does not exist, is not an IP node or has no address in the project, the mask or
        /// router is not valid for it, or TIA Portal refused them.
        /// </exception>
        public NodeIpSettings SetNodeIpSettings(string deviceItemPath, string nodeName, NodeIpSettings settings, string backupDirectory)
        {
            _logger?.LogInformation("Setting the mask and router of {Node} on {Device}...", nodeName, deviceItemPath);

            try
            {
                var node = RequireIpNodeForSettings(deviceItemPath, nodeName, backupDirectory);
                var mask = IpSettingsRule.RequireSubnetMask(settings.SubnetMask);

                if (settings.UsesRouter)
                {
                    IpSettingsRule.RequireRouter(settings.RouterAddress, RequireIpNodeAddress(node, deviceItemPath), mask);
                }

                RecordNetworkBefore(backupDirectory);
                DeviceParameterWriter.Write(backupDirectory, deviceItemPath + "/" + node.Name, EngineeringAttributeReader.Read(node));

                WriteIpSettings(node, new NodeIpSettings(IpSettingsRule.Format(mask), settings.RouterAddress.Trim()));

                return ReadIpSettings(node);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, deviceItemPath, backupDirectory, "SetNodeIpSettings");
            }
        }

        /// <remarks>Finds the node without recording anything, so a call refused by the checks leaves no backup behind.</remarks>
        private Node RequireIpNodeForSettings(string deviceItemPath, string nodeName, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes the network");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before changing the network");
            }

            var deviceItem = FindDeviceItem(deviceItemPath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {deviceItemPath}");
            var node = RequireNode(deviceItem, deviceItemPath, nodeName);

            return node.NodeType == NetType.Ethernet
                ? node
                : throw new PortalException(PortalErrorCode.InvalidState, $"'{node.Name}' on '{deviceItemPath}' is a {node.NodeType} node: it has no subnet mask or router");
        }

        /// <remarks>A node whose address is set at the device or by DHCP holds none in the project, and its router cannot be checked.</remarks>
        private static string RequireIpNodeAddress(Node node, string deviceItemPath)
        {
            var address = node.GetAttribute(NodeAddressAttribute)?.ToString() ?? string.Empty;

            return address.Length > 0
                ? address
                : throw new PortalException(PortalErrorCode.InvalidState, $"'{node.Name}' on '{deviceItemPath}' has no address in the project, so its router cannot be checked. Set one first (SetDeviceAddress).");
        }

        /// <remarks>
        /// A refused value is the caller's mistake, so it comes back as invalid input, as SetAddress does. What was
        /// already written is put back first, and the message says whether that worked.
        /// </remarks>
        private static void WriteIpSettings(Node node, NodeIpSettings wanted)
        {
            var before = ReadIpSettings(node);

            try
            {
                ApplyIpSettings(node, wanted);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"TIA Portal refused mask {wanted.SubnetMask} or router '{wanted.RouterAddress}' for node '{node.Name}': {failure.Message}. {RestoreIpSettings(node, before)}",
                    null,
                    failure);
            }
        }

        private static void ApplyIpSettings(Node node, NodeIpSettings settings)
        {
            node.SetAttribute(SubnetMaskAttribute, settings.SubnetMask);
            node.SetAttribute(UseRouterAttribute, settings.UsesRouter);

            if (settings.UsesRouter)
            {
                node.SetAttribute(RouterAddressAttribute, settings.RouterAddress);
            }
        }

        /// <remarks>The one place a failure is caught and not rethrown: it is reported in the message of the failure being thrown.</remarks>
        private static string RestoreIpSettings(Node node, NodeIpSettings before)
        {
            try
            {
                ApplyIpSettings(node, before);

                return $"The node was put back to mask {before.SubnetMask} and {(before.UsesRouter ? "router " + before.RouterAddress : "no router")}.";
            }
            catch (Exception restoreFailure)
            {
                return $"Putting it back failed too ({restoreFailure.Message}): the node may hold part of the change; the backup has what it held before.";
            }
        }

        private static NodeIpSettings ReadIpSettings(Node node)
        {
            var usesRouter = node.GetAttribute(UseRouterAttribute) is bool flag && flag;

            return new NodeIpSettings(
                node.GetAttribute(SubnetMaskAttribute)?.ToString() ?? string.Empty,
                usesRouter ? node.GetAttribute(RouterAddressAttribute)?.ToString() ?? string.Empty : string.Empty);
        }
    }
}
