using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Every subnet an interface in this project can be on.
    /// </summary>
    /// <remarks>
    /// **Not the same set as <c>Project.Subnets</c>, and the difference was found by running it.**
    /// The topology reported an interface on <c>PC-internes Subnetz_1</c> and the project's subnet
    /// composition did not contain it: a PC station's internal subnet belongs to the station, not
    /// to the project. Reading only the composition produced a tool that named a subnet the tool
    /// beside it swore did not exist.
    ///
    /// That is the failure of 2026-09-05 repeated with different types — a read tool printing a
    /// name the write tool cannot resolve — so the fix is the one that makes it impossible rather
    /// than unlikely: **both sides ask this**. What GetSubnets lists is what ConnectDeviceToSubnet
    /// can find, because it is one enumeration with one definition.
    ///
    /// Deduplicated by name, because the name is what every caller keys on. Two subnets sharing a
    /// name would be indistinguishable to anybody using these tools anyway, and the composition
    /// wins, since that is the one the project itself owns.
    /// </remarks>
    public sealed class SubnetLookup
    {
        /// <summary>Collects the subnets of a project and of its devices' interfaces.</summary>
        /// <param name="subnets">The project's subnet composition.</param>
        /// <param name="devices">The project's devices, walked for connected interfaces.</param>
        /// <returns>Every distinct subnet, the project's own first.</returns>
        /// <exception cref="PortalException">Either argument is missing.</exception>
        public IReadOnlyList<Subnet> All(SubnetComposition subnets, IEnumerable<Device> devices)
        {
            if (subnets == null || devices == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "subnets and devices are required");
            }

            var byName = new Dictionary<string, Subnet>(StringComparer.OrdinalIgnoreCase);

            foreach (var subnet in subnets.Concat(ConnectedSubnets(devices)))
            {
                if (!byName.ContainsKey(subnet.Name))
                {
                    byName.Add(subnet.Name, subnet);
                }
            }

            return byName.Values.ToList();
        }

        private static IEnumerable<Subnet> ConnectedSubnets(IEnumerable<Device> devices)
        {
            return devices
                .SelectMany(device => device.DeviceItems.Cast<DeviceItem>())
                .SelectMany(Interfaces)
                .SelectMany(networkInterface => networkInterface.Nodes.Cast<Node>())
                .Select(node => node.ConnectedSubnet)
                .Where(subnet => subnet != null)
                .Select(subnet => subnet!);
        }

        /// <remarks>
        /// The same recursion the topology reader walks, and for the same reason: a CPU's interface
        /// is a child device item, so asking the CPU for the service returns nothing.
        /// </remarks>
        private static IEnumerable<NetworkInterface> Interfaces(DeviceItem deviceItem)
        {
            var own = deviceItem.GetService<NetworkInterface>();
            if (own != null)
            {
                yield return own;
            }

            foreach (var nested in deviceItem.DeviceItems)
            {
                foreach (var networkInterface in Interfaces(nested))
                {
                    yield return networkInterface;
                }
            }
        }
    }
}
