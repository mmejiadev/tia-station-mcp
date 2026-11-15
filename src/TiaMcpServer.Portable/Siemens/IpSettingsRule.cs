using System.Globalization;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Checks a subnet mask and a router address before they reach a node.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21: a CPU's Ethernet node holds <c>SubnetMask</c>, <c>UseRouter</c> and
    /// <c>RouterAddress</c>, all writable. Checked here, without TIA Portal, so a mask with a hole in it or a router
    /// outside the node's own subnet is refused with a reason instead of being stored and found later as a compile
    /// error or a PLC that never reaches its gateway. IPv4 only, written as four decimal numbers: the forms
    /// <c>IPAddress.Parse</c> also takes (<c>10.1</c>, octal, hexadecimal) are refused, as TIA Portal shows none of them.
    /// </remarks>
    public static class IpSettingsRule
    {
        private const int OctetCount = 4;
        private const int MaximumOctet = 255;
        private const int MaximumOctetDigits = 3;
        private const int BitsPerOctet = 8;
        private const uint OctetMask = 0xFF;

        /// <summary>The fewest host addresses a node's subnet needs: the node and one partner, besides network and broadcast.</summary>
        private const uint SmallestUsableHostPart = 3;

        /// <summary>Refuses a subnet mask that is not a run of ones followed by zeros, or leaves no room for two hosts.</summary>
        /// <param name="subnetMask">The mask, e.g. <c>255.255.255.0</c>.</param>
        /// <returns>The mask as a 32-bit number.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is blank, not IPv4, all zeros, not contiguous, or /31 or /32.</exception>
        public static uint RequireSubnetMask(string? subnetMask)
        {
            var mask = RequireIpv4(subnetMask, "subnetMask");
            var inverted = ~mask;

            if (mask == 0 || (inverted & (inverted + 1)) != 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{subnetMask}' is not a subnet mask: it must be ones followed by zeros, e.g. 255.255.255.0 or 255.255.0.0");
            }

            if (inverted < SmallestUsableHostPart)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{subnetMask}' leaves no room on the subnet for the node and a partner: use 255.255.255.252 or wider");
            }

            return mask;
        }

        /// <summary>Refuses a router that is not an address inside the node's own subnet.</summary>
        /// <param name="routerAddress">The router, e.g. <c>192.168.0.254</c>.</param>
        /// <param name="nodeAddress">The address the node holds.</param>
        /// <param name="subnetMask">The mask the node will hold, already checked by <see cref="RequireSubnetMask"/>.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is not IPv4, is the node's own address, the subnet's network or broadcast address, or lies outside the subnet.</exception>
        public static void RequireRouter(string? routerAddress, string nodeAddress, uint subnetMask)
        {
            var router = RequireIpv4(routerAddress, "routerAddress");
            var node = RequireIpv4(nodeAddress, "the node's address");

            if (router == node)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The router cannot be the node's own address, {nodeAddress}");
            }

            if ((router & subnetMask) != (node & subnetMask))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The router {routerAddress} is outside the node's subnet ({nodeAddress} with mask {Format(subnetMask)}): a PLC only reaches a router on its own subnet");
            }

            var hostPart = router & ~subnetMask;

            if (hostPart == 0 || hostPart == ~subnetMask)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The router {routerAddress} is the subnet's {(hostPart == 0 ? "network" : "broadcast")} address, which no device can hold");
            }
        }

        /// <summary>Writes a 32-bit address in dotted form.</summary>
        /// <param name="address">The address.</param>
        /// <returns>E.g. <c>255.255.255.0</c>.</returns>
        public static string Format(uint address)
        {
            return string.Join(".", Enumerable.Range(0, OctetCount).Select(index => (address >> (BitsPerOctet * (OctetCount - 1 - index))) & OctetMask));
        }

        private static uint RequireIpv4(string? text, string what)
        {
            var parts = (text ?? string.Empty).Trim().Split('.');

            if (parts.Length != OctetCount || parts.Any(part => !IsOctet(part)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be an IPv4 address written as four numbers, e.g. 192.168.0.1; got '{text}'");
            }

            return parts.Aggregate(0u, (sum, part) => (sum << BitsPerOctet) | uint.Parse(part, CultureInfo.InvariantCulture));
        }

        private static bool IsOctet(string part)
        {
            return part.Length > 0
                && part.Length <= MaximumOctetDigits
                && part.All(character => character >= '0' && character <= '9')
                && int.Parse(part, CultureInfo.InvariantCulture) <= MaximumOctet;
        }
    }
}
