using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the subnets of a project: the wires, rather than the interfaces plugged into them.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="NetworkTopologyReader"/>, and the read a connect has to start
    /// from. The topology answers "what is this interface attached to"; this answers "what is there
    /// to attach it to", which is the question whose answer cannot be guessed: a subnet name is
    /// free text chosen by whoever created it, and connecting to a name that does not exist creates
    /// nothing -- it fails.
    ///
    /// The network type is read because it is the constraint that decides the outcome. An Ethernet
    /// node cannot join a PROFIBUS subnet, and TIA Portal's own refusal for that does not say so.
    /// </remarks>
    public sealed class SubnetReader
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a subnet reader.</summary>
        /// <param name="logger">Optional logger.</param>
        public SubnetReader(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Describes every subnet an interface in the project can be on.</summary>
        /// <param name="subnets">The subnets, as <see cref="SubnetLookup"/> enumerates them.</param>
        /// <returns>One entry per subnet.</returns>
        /// <exception cref="PortalException">No subnets were given.</exception>
        /// <remarks>
        /// It takes the list rather than the project's composition on purpose: a PC station's
        /// internal subnet is not in that composition, and reading it directly made this tool deny
        /// the existence of a subnet the topology was printing. See <see cref="SubnetLookup"/>.
        /// </remarks>
        public IReadOnlyList<SubnetInfo> Read(IReadOnlyList<Subnet> subnets)
        {
            if (subnets == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "subnets is required");
            }

            var described = subnets.Select(Describe).ToList();

            _logger?.LogInformation("Subnets: {Count} found", described.Count);

            return described;
        }

        private static SubnetInfo Describe(Subnet subnet)
        {
            return new SubnetInfo(
                subnet.Name,
                DescribeNetworkType(subnet.NetType),
                subnet.Nodes.Select(node => node.Name).ToList(),
                subnet.IoSystems.Select(ioSystem => ioSystem.Name).ToList());
        }

        /// <remarks>
        /// The same treatment the topology reader gives an unnamed net type, and for the same
        /// reason: a bare "16" reads like data rather than like a gap in the published enum.
        /// </remarks>
        private static string DescribeNetworkType(NetType netType)
        {
            return Enum.IsDefined(typeof(NetType), netType)
                ? netType.ToString()
                : $"Unknown({(int)netType})";
        }
    }
}
