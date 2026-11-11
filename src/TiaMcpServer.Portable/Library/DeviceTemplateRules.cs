using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>What a device group template must be, and what placing one must not collide with.</summary>
    /// <remarks>
    /// Measured 2026-11-08 on TIA Portal V20: an integrated HMI connection cannot be made through Openness, but a device
    /// group holding one made by hand becomes a master copy that brings it along. Placing it again renames everything
    /// without a word (<c>Group_2</c>, <c>PLC_2</c>, <c>HMI_RT_2</c>) and repeats the same addresses.
    /// </remarks>
    public static class DeviceTemplateRules
    {
        private const string DeviceGroupType = "DeviceUserGroup";

        /// <summary>Throws unless the name can be a template's.</summary>
        /// <param name="templateName">The name.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a blank name or one with a folder.</exception>
        public static void CheckTemplateName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName) || templateName.IndexOf('/') >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"A template's name is required and has no folder in it; got '{templateName}'");
            }
        }

        /// <summary>Throws unless a master copy holds exactly one device group.</summary>
        /// <param name="templateName">The master copy.</param>
        /// <param name="contentTypes">The Openness type names of what it holds.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for anything else.</exception>
        public static void RequireDeviceGroup(string templateName, IReadOnlyCollection<string> contentTypes)
        {
            if (contentTypes == null || contentTypes.Count != 1 || contentTypes.Single() != DeviceGroupType)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{templateName}' is not a device group template: it holds {string.Join(", ", contentTypes ?? Array.Empty<string>())}");
            }
        }

        /// <summary>Throws unless a subnet name was given.</summary>
        /// <param name="subnetName">The name.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a blank one.</exception>
        public static void CheckSubnetName(string subnetName)
        {
            if (string.IsNullOrWhiteSpace(subnetName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "subnetName is required: the integrated connection needs both devices on one subnet");
            }
        }

        /// <summary>Throws when a device placed would repeat an address the subnet already holds.</summary>
        /// <param name="subnetName">The subnet.</param>
        /// <param name="held">The addresses on it.</param>
        /// <param name="placed">The addresses of the devices being placed.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the addresses.</exception>
        /// <remarks>A node with no address yet repeats nothing: two empty addresses are not one address used twice.</remarks>
        public static void CheckAddressesFree(string subnetName, IEnumerable<string> held, IEnumerable<string> placed)
        {
            var repeated = placed.Where(address => !string.IsNullOrWhiteSpace(address)).Intersect(held, StringComparer.Ordinal).ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The subnet '{subnetName}' already holds {string.Join(", ", repeated)}, which this template's devices also have; give another subnet, and change the addresses with SetDeviceAddress before joining them");
            }
        }
    }
}
