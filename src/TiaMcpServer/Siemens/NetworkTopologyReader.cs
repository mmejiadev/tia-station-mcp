using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the network layout of a project: which devices exist, how they are wired and at what
    /// addresses.
    /// </summary>
    /// <remarks>
    /// Read before write. Creating a PROFINET IO system or writing code that addresses remote IO
    /// both start from knowing what is already there, and this is also the piece missing from
    /// version control: the snapshot covers the program but not the network the program runs on.
    ///
    /// Devices nest — a rack holds a CPU which holds interfaces — so the walk is recursive. An
    /// interface that exists but is attached to no subnet is reported rather than skipped: an
    /// unwired interface is a common and otherwise silent reason a download or an IO connection
    /// fails.
    /// </remarks>
    public sealed class NetworkTopologyReader
    {
        private const string AddressAttribute = "Address";
        private const string PathSeparator = "/";

        private readonly ILogger? _logger;

        /// <summary>Creates a topology reader.</summary>
        /// <param name="logger">Optional logger.</param>
        public NetworkTopologyReader(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Reads every network attachment point in a set of devices.</summary>
        /// <param name="devices">The project's devices.</param>
        /// <returns>One entry per interface, connected or not.</returns>
        public IReadOnlyList<NetworkNodeInfo> Read(IEnumerable<Device> devices)
        {
            if (devices == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "devices is required");
            }

            var nodes = new List<NetworkNodeInfo>();

            foreach (var device in devices)
            {
                foreach (var deviceItem in device.DeviceItems)
                {
                    Collect(deviceItem, device.Name, nodes);
                }
            }

            _logger?.LogInformation("Network topology: {Count} interface(s) found", nodes.Count);

            return nodes;
        }

        private void Collect(DeviceItem deviceItem, string parentPath, List<NetworkNodeInfo> nodes)
        {
            var path = parentPath + PathSeparator + deviceItem.Name;

            AddInterfaces(deviceItem, path, nodes);

            foreach (var nested in deviceItem.DeviceItems)
            {
                Collect(nested, path, nodes);
            }
        }

        private static void AddInterfaces(DeviceItem deviceItem, string path, List<NetworkNodeInfo> nodes)
        {
            var networkInterface = deviceItem.GetService<NetworkInterface>();
            if (networkInterface == null)
            {
                return;
            }

            foreach (var node in networkInterface.Nodes)
            {
                nodes.Add(new NetworkNodeInfo(
                    path,
                    node.Name,
                    node.NodeType.ToString(),
                    ReadAddress(node),
                    node.ConnectedSubnet?.Name ?? string.Empty));
            }
        }

        private static string ReadAddress(Node node)
        {
            try
            {
                return node.GetAttribute(AddressAttribute)?.ToString() ?? string.Empty;
            }
            catch (Exception)
            {
                // Not every node type carries an Address attribute; a PROFIBUS node uses a
                // station number instead. Absence is normal here, not an error worth raising.
                return string.Empty;
            }
        }
    }
}
