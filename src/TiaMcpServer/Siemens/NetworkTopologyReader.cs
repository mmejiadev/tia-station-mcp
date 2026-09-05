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
                var root = AddressableDeviceName(device);

                foreach (var deviceItem in device.DeviceItems)
                {
                    Collect(deviceItem, root, nodes);
                }
            }

            _logger?.LogInformation("Network topology: {Count} interface(s) found", nodes.Count);

            return nodes;
        }

        /// <summary>
        /// The device's name when it can appear in a path, and nothing when it cannot.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <returns>The name, or an empty string.</returns>
        /// <remarks>
        /// **A name containing the separator cannot be part of a separator-joined path.** A hardware
        /// station is called <c>S7-1500/ET200MP-Station_3</c> by Openness -- a name TIA Portal's own
        /// IDE never shows -- and joining it with '/' produces something no reader can split back:
        /// four segments where the first two are one name.
        ///
        /// This is why paths from this reader could not be handed to any tool that takes one. It went
        /// unnoticed until 2026-09-05 because nothing tried: the topology was printed for a person,
        /// and the first write tool aimed with those two columns failed on its first run.
        ///
        /// Dropping the name is not a loss. The lookup already ignores it for exactly these devices
        /// and matches the device item instead, so <c>PLC_0/PROFINET-Schnittstelle_1</c> is what both
        /// sides agree on. A PC station, whose name has no separator and *is* shown in the IDE, keeps
        /// its prefix.
        /// </remarks>
        private static string AddressableDeviceName(Device device)
        {
            var name = device.Name ?? string.Empty;

            return name.Contains(PathSeparator) ? string.Empty : name;
        }

        private void Collect(DeviceItem deviceItem, string parentPath, List<NetworkNodeInfo> nodes)
        {
            var path = parentPath.Length == 0
                ? deviceItem.Name
                : parentPath + PathSeparator + deviceItem.Name;

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
                    DescribeNetworkType(node.NodeType),
                    ReadAddress(node),
                    node.ConnectedSubnet?.Name ?? string.Empty));
            }
        }

        /// <remarks>
        /// TIA returns net type values the published enum has no name for — a real project shows
        /// a bare "16" — and <c>ToString()</c> renders those as a number, which reads like data
        /// rather than like a gap. Saying "Unknown(16)" makes it obvious that the value is real
        /// but unnamed.
        /// </remarks>
        private static string DescribeNetworkType(NetType netType)
        {
            return Enum.IsDefined(typeof(NetType), netType)
                ? netType.ToString()
                : $"Unknown({(int)netType})";
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
