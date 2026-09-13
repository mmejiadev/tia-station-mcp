using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Creates subnets and attaches interfaces to them: the wiring underneath every IO system.
    /// </summary>
    /// <remarks>
    /// <c>NetworkConfigurator</c> creates a subnet as a side effect of building an IO system, and
    /// names it after the IO system because it has nothing better to go on. That is enough for one
    /// controller and wrong for a cell: four stations that each got their own implicit subnet are
    /// four networks that cannot see each other, and nothing in the project says so.
    ///
    /// This is the explicit half. It creates a subnet with the name the caller chose, and connects
    /// interfaces to a subnet that already exists.
    ///
    /// **It never rewires.** A node already attached to a subnet is refused rather than moved,
    /// because moving one is how a working network silently becomes a broken one, and because the
    /// caller that wanted it moved can say so by disconnecting first. The one exception is
    /// reconnecting a node to the subnet it is already on, which changes nothing and therefore
    /// succeeds: a write that cannot be run twice is a write no retry can recover.
    /// </remarks>
    public sealed class SubnetConfigurator
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a subnet configurator.</summary>
        /// <param name="logger">Optional logger.</param>
        public SubnetConfigurator(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Creates a subnet and connects one interface to it.</summary>
        /// <param name="node">The node that will be its first member.</param>
        /// <param name="subnets">The project's subnets, checked for the name.</param>
        /// <param name="subnetName">The name to give it.</param>
        /// <returns>The subnet the node sits on afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// The name is missing or taken, the node is already on a subnet, or TIA Portal refused.
        /// </exception>
        /// <remarks>
        /// The subnet's network type is not a parameter, and cannot be: it comes from the interface,
        /// because <c>Node.CreateAndConnectToSubnet</c> creates the kind of subnet that node can
        /// join. That is why it is preferred over <c>SubnetComposition.Create</c>, which takes a
        /// type identifier string nobody can produce without looking it up.
        /// </remarks>
        public string Create(Node node, IReadOnlyList<Subnet> subnets, string subnetName)
        {
            RequireArguments(node, subnets, subnetName);

            if (node.ConnectedSubnet != null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{node.Name}' is already on subnet '{node.ConnectedSubnet.Name}'. Creating another " +
                    "would rewire it; disconnect it in TIA Portal first if that is what you want.");
            }

            if (Find(subnets, subnetName) != null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"A subnet called '{subnetName}' already exists. Connect '{node.Name}' to it instead of creating a second one.");
            }

            var created = CreateAndConnect(node, subnetName);

            _logger?.LogInformation("Subnet {Subnet} created from node {Node}", created, node.Name);

            return created;
        }

        /// <summary>Connects one interface to a subnet that already exists.</summary>
        /// <param name="node">The node to attach.</param>
        /// <param name="subnets">The project's subnets.</param>
        /// <param name="subnetName">The subnet to attach it to.</param>
        /// <returns>The subnet the node sits on afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// The name is missing or unknown, the node is on a different subnet, the network types do
        /// not match, or TIA Portal refused.
        /// </exception>
        public string Connect(Node node, IReadOnlyList<Subnet> subnets, string subnetName)
        {
            RequireArguments(node, subnets, subnetName);

            var subnet = Find(subnets, subnetName)
                ?? throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No subnet called '{subnetName}'. The project has: {NameThem(subnets)}");

            if (IsAlreadyOn(node, subnet))
            {
                return subnet.Name;
            }

            RequireUnconnected(node, subnet);
            RequireSameNetworkType(node, subnet);

            ConnectTo(node, subnet);

            _logger?.LogInformation("Node {Node} connected to subnet {Subnet}", node.Name, subnet.Name);

            return node.ConnectedSubnet?.Name ?? subnet.Name;
        }

        private static void RequireArguments(Node node, IReadOnlyList<Subnet> subnets, string subnetName)
        {
            if (node == null || subnets == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "node and subnets are required");
            }

            if (string.IsNullOrWhiteSpace(subnetName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "subnetName is required");
            }
        }

        /// <remarks>
        /// Reconnecting a node to the subnet it is already on is the no-op that makes this write
        /// idempotent, which the repository requires of every write. Openness throws for it.
        /// </remarks>
        private static bool IsAlreadyOn(Node node, Subnet subnet)
        {
            return node.ConnectedSubnet != null
                && string.Equals(node.ConnectedSubnet.Name, subnet.Name, StringComparison.Ordinal);
        }

        private static void RequireUnconnected(Node node, Subnet subnet)
        {
            if (node.ConnectedSubnet == null)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"'{node.Name}' is on subnet '{node.ConnectedSubnet.Name}'. Moving it to '{subnet.Name}' " +
                "would unwire whatever it talks to now; disconnect it in TIA Portal first.");
        }

        /// <remarks>
        /// Checked here rather than left to Openness because Openness reports it as a generic
        /// failure. The mismatch is the most likely reason a connect fails, and naming both types
        /// turns an unexplained refusal into an obvious mistake.
        /// </remarks>
        private static void RequireSameNetworkType(Node node, Subnet subnet)
        {
            if (node.NodeType == subnet.NetType)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{node.Name}' is a {node.NodeType} interface and '{subnet.Name}' is a {subnet.NetType} subnet. " +
                "An interface can only join a subnet of its own network type.");
        }

        private static string CreateAndConnect(Node node, string subnetName)
        {
            try
            {
                return node.CreateAndConnectToSubnet(subnetName).Name;
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"TIA Portal refused to create subnet '{subnetName}' from '{node.Name}': {failure.Message}",
                    null,
                    failure);
            }
        }

        private static void ConnectTo(Node node, Subnet subnet)
        {
            try
            {
                node.ConnectToSubnet(subnet);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"TIA Portal refused to connect '{node.Name}' to '{subnet.Name}': {failure.Message}",
                    null,
                    failure);
            }
        }

        private static Subnet? Find(IReadOnlyList<Subnet> subnets, string subnetName)
        {
            return subnets.FirstOrDefault(
                subnet => string.Equals(subnet.Name, subnetName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NameThem(IReadOnlyList<Subnet> subnets)
        {
            var names = subnets.Select(subnet => subnet.Name).ToList();

            return names.Count == 0 ? "none" : string.Join(", ", names);
        }
    }
}
