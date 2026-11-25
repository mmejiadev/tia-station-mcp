using Siemens.Engineering;
using Siemens.Engineering.HW;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>Where a device's paths start: its device groups, outermost first, then its name.</summary>
    /// <remarks>
    /// The path lookup reads a device in a group as <c>Group/Device/Item</c>, and the software paths are written that way,
    /// but the network and hardware readers started every path at the device's own name. A device in a group then had
    /// paths nothing resolved: every network tool answered "Device item not found" for the paths GetNetworkTopology
    /// had just given (measured 2026-11-24 on TIA Portal V21, on a group built through Openness). One rule here, as
    /// <see cref="ProjectPath.AddressableDeviceName"/> is one rule, so the readers cannot come to disagree.
    /// </remarks>
    internal static class DevicePathRoot
    {
        /// <summary>The path a device's items are written under.</summary>
        /// <param name="device">The device.</param>
        /// <returns><c>Group/Device</c>; the group alone when the device's name cannot be a segment; the name alone outside a group.</returns>
        public static string Of(Device device)
        {
            var groups = GroupsAbove(device);
            var name = ProjectPath.AddressableDeviceName(device.Name);

            if (name.Length > 0)
            {
                groups.Add(name);
            }

            return string.Join("/", groups);
        }

        private static List<string> GroupsAbove(Device device)
        {
            var names = new List<string>();

            for (IEngineeringObject? current = device.Parent; current is DeviceUserGroup group; current = group.Parent)
            {
                names.Insert(0, group.Name);
            }

            return names;
        }
    }
}
