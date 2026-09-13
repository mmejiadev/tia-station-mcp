using Siemens.Engineering.HW;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the address ranges of a module and of everything nested inside it.
    /// </summary>
    /// <remarks>
    /// Openness hangs addresses off the device item that owns them, and a module's channels are
    /// often nested one level down, so the walk is recursive for the reason every walk in this
    /// server is: a reader that stopped at the top would report a card with no addresses at all.
    /// </remarks>
    public static class IoAddressReader
    {
        /// <summary>Reads every address range of a device item and its children.</summary>
        /// <param name="deviceItem">The module to read.</param>
        /// <param name="modulePath">The path to report the ranges under.</param>
        /// <returns>The ranges, in the order Openness holds them.</returns>
        public static IReadOnlyList<IoAddressInfo> Read(DeviceItem deviceItem, string modulePath)
        {
            if (deviceItem == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "deviceItem is required");
            }

            var ranges = new List<IoAddressInfo>();

            Collect(deviceItem, modulePath, ranges);

            return ranges;
        }

        /// <summary>The address ranges of one device item, ignoring what is nested inside it.</summary>
        /// <param name="deviceItem">The module to read.</param>
        /// <returns>Its own ranges.</returns>
        /// <remarks>
        /// What the write aims at. The read walks children so that a caller sees the whole card;
        /// the write must not, or "set the input address of this module" would silently pick a
        /// channel of it.
        /// </remarks>
        public static IReadOnlyList<Address> Own(DeviceItem deviceItem)
        {
            return deviceItem.Addresses.ToList();
        }

        private static void Collect(DeviceItem deviceItem, string modulePath, List<IoAddressInfo> ranges)
        {
            foreach (var address in deviceItem.Addresses)
            {
                ranges.Add(new IoAddressInfo(
                    modulePath,
                    address.IoType.ToString(),
                    address.StartAddress,
                    address.Length));
            }

            foreach (var nested in deviceItem.DeviceItems)
            {
                Collect(nested, ProjectPath.Join(modulePath, nested.Name), ranges);
            }
        }
    }
}
