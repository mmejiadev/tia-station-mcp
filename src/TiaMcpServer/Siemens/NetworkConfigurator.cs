using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Builds PROFINET IO systems: creates them on a controller and attaches IO devices to them.
    /// </summary>
    /// <remarks>
    /// The write side of the network. Three Openness calls carry it —
    /// <c>Node.ConnectToSubnet</c>, <c>IoController.CreateIoSystem</c> and
    /// <c>IoConnector.ConnectToIoSystem</c> — but the objects they need live in different places:
    /// a controller exposes an <c>IoController</c> on its interface, an IO device exposes an
    /// <c>IoConnector</c> on its own, and neither is reachable from the device itself.
    ///
    /// PROFIBUS uses the same three concepts, so what works here should carry over with little
    /// more than a different subnet type.
    /// </remarks>
    public sealed class NetworkConfigurator
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a network configurator.</summary>
        /// <param name="logger">Optional logger.</param>
        public NetworkConfigurator(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a PROFINET IO system on a controller's interface, attaching it to a subnet.
        /// </summary>
        /// <param name="controller">The CPU device item that will act as IO controller.</param>
        /// <param name="ioSystemName">Name for the IO system.</param>
        /// <returns>The name of the subnet the IO system ended up on.</returns>
        /// <exception cref="PortalException">The device item is not an IO controller.</exception>
        public string CreateIoSystem(DeviceItem controller, string ioSystemName)
        {
            if (string.IsNullOrWhiteSpace(ioSystemName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "ioSystemName is required");
            }

            var ioController = FindIoController(controller)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{controller.Name}' has no interface that can act as an IO controller");

            var subnet = EnsureSubnet(controller, ioSystemName);

            var ioSystem = ioController.CreateIoSystem(ioSystemName);

            _logger?.LogInformation("IO system {IoSystem} created on {Controller}, subnet {Subnet}", ioSystem.Name, controller.Name, subnet);

            return subnet;
        }

        /// <summary>Attaches an IO device to an existing IO system.</summary>
        /// <param name="device">The IO device to attach.</param>
        /// <param name="ioSystemName">The IO system to attach it to.</param>
        /// <exception cref="PortalException">The device has no IO connector, or the IO system is unknown.</exception>
        public void AssignToIoSystem(DeviceItem device, string ioSystemName)
        {
            var connector = FindIoConnector(device)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{device.Name}' has no interface that can join an IO system");

            var ioSystem = FindIoSystem(device, ioSystemName)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"No IO system named '{ioSystemName}' on any subnet this device can reach");

            connector.ConnectToIoSystem(ioSystem);

            _logger?.LogInformation("{Device} attached to IO system {IoSystem}", device.Name, ioSystemName);
        }

        private static string EnsureSubnet(DeviceItem deviceItem, string preferredName)
        {
            var node = FindNode(deviceItem)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{deviceItem.Name}' has no network node to attach");

            // An interface already on a subnet keeps it: rewiring a working network because a name
            // did not match would be a destructive surprise.
            if (node.ConnectedSubnet != null)
            {
                return node.ConnectedSubnet.Name;
            }

            return node.CreateAndConnectToSubnet(preferredName + "_subnet").Name;
        }

        private static IoSystem? FindIoSystem(DeviceItem deviceItem, string ioSystemName)
        {
            var node = FindNode(deviceItem);

            return node?.ConnectedSubnet?.IoSystems.FirstOrDefault(
                candidate => string.Equals(candidate.Name, ioSystemName, StringComparison.OrdinalIgnoreCase));
        }

        private static Node? FindNode(DeviceItem deviceItem)
        {
            return Interfaces(deviceItem).SelectMany(network => network.Nodes.Cast<Node>()).FirstOrDefault();
        }

        private static IoController? FindIoController(DeviceItem deviceItem)
        {
            return Interfaces(deviceItem).SelectMany(network => network.IoControllers.Cast<IoController>()).FirstOrDefault();
        }

        private static IoConnector? FindIoConnector(DeviceItem deviceItem)
        {
            return Interfaces(deviceItem).SelectMany(network => network.IoConnectors.Cast<IoConnector>()).FirstOrDefault();
        }

        /// <summary>
        /// The network interfaces of a device item and of everything nested inside it.
        /// </summary>
        /// <remarks>
        /// A CPU's PROFINET interface is a child device item, not the CPU itself, so asking the
        /// CPU for the service returns nothing. This is the same nesting the topology reader walks.
        /// </remarks>
        private static System.Collections.Generic.IEnumerable<NetworkInterface> Interfaces(DeviceItem deviceItem)
        {
            var own = deviceItem.GetService<NetworkInterface>();
            if (own != null)
            {
                yield return own;
            }

            foreach (var nested in deviceItem.DeviceItems)
            {
                foreach (var network in Interfaces(nested))
                {
                    yield return network;
                }
            }
        }
    }
}
