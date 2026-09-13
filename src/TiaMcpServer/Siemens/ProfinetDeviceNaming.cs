using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads and writes the PROFINET device name of a network node.
    /// </summary>
    /// <remarks>
    /// The PROFINET device name, not the address, is how an IO controller finds its IO devices:
    /// the controller resolves the name over DCP at start-up and only then talks IP. A device
    /// whose name in the project differs from the name burned into the hardware never joins the
    /// IO system, and the diagnostic for it is a station that stays dark.
    ///
    /// TIA Portal generates the name from the interface by default, which is why setting one by
    /// hand means turning that generation off first. This class does both, in that order, because
    /// writing the name while generation is on is silently overwritten.
    /// </remarks>
    public sealed class ProfinetDeviceNaming
    {
        private const string NameAttribute = "PnDeviceName";
        private const string AutoGenerationAttribute = "PnDeviceNameAutoGeneration";

        private readonly ILogger? _logger;

        /// <summary>Creates a PROFINET naming reader and writer.</summary>
        /// <param name="logger">Optional logger.</param>
        public ProfinetDeviceNaming(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>The PROFINET device name of a node, or an empty string when it has none.</summary>
        /// <param name="node">The node to read.</param>
        /// <returns>The name, or an empty string.</returns>
        /// <remarks>
        /// A PROFIBUS node has no such attribute, and neither does an Ethernet node that is not on
        /// a PROFINET-capable interface. Absence is normal here, exactly as it is for the address
        /// of a PROFIBUS node, so it is reported as "nothing" rather than raised.
        /// </remarks>
        public static string Read(Node node)
        {
            if (node == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "node is required");
            }

            if (!HasAttribute(node, NameAttribute))
            {
                return string.Empty;
            }

            try
            {
                return node.GetAttribute(NameAttribute)?.ToString() ?? string.Empty;
            }
            catch (Exception)
            {
                // The attribute is advertised but unreadable on this node type. Same judgement as
                // above: a name we cannot read is not a failure of the tool asking for it.
                return string.Empty;
            }
        }

        /// <summary>Sets the PROFINET device name of a node.</summary>
        /// <param name="node">The node to name.</param>
        /// <param name="deviceName">The name to set.</param>
        /// <returns>The name the node holds afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// The node is not a PROFINET node, or TIA Portal refused the name.
        /// </exception>
        public string Set(Node node, string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "deviceName is required");
            }

            RequireProfinetNode(node);

            StopAutoGeneration(node);

            SetName(node, deviceName);

            var stored = Read(node);

            _logger?.LogInformation("Node {Node} is now called {DeviceName} on PROFINET", node.Name, stored);

            return stored;
        }

        /// <remarks>
        /// Names the attributes the node does have. "Not a PROFINET node" on its own sends somebody
        /// back to TIA Portal to work out which of its interfaces is the PROFINET one.
        /// </remarks>
        private static void RequireProfinetNode(Node node)
        {
            if (HasAttribute(node, NameAttribute))
            {
                return;
            }

            var present = string.Join(", ", node.GetAttributeInfos().Select(information => information.Name));

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"Node '{node.Name}' has no PROFINET device name to set, so it is not a PROFINET node. " +
                $"It has: {present}");
        }

        /// <remarks>
        /// TIA derives the device name from the interface unless told not to, and a name written
        /// while it is deriving does not survive. Turning generation off is therefore part of
        /// setting a name by hand, not a separate decision the caller could have made.
        /// </remarks>
        private void StopAutoGeneration(Node node)
        {
            if (!HasAttribute(node, AutoGenerationAttribute))
            {
                return;
            }

            if (node.GetAttribute(AutoGenerationAttribute) is bool isGenerated && !isGenerated)
            {
                return;
            }

            node.SetAttribute(AutoGenerationAttribute, false);

            _logger?.LogInformation("Automatic PROFINET naming turned off on {Node}", node.Name);
        }

        /// <remarks>
        /// A refused name is the caller's mistake, so it comes back as invalid input. PROFINET
        /// names are DNS labels: lowercase letters, digits and hyphens, no underscores and no
        /// spaces, which is not what a station is usually called in a project.
        /// </remarks>
        private static void SetName(Node node, string deviceName)
        {
            var before = Read(node);

            try
            {
                node.SetAttribute(NameAttribute, deviceName);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"TIA Portal refused '{deviceName}' as the PROFINET device name of '{node.Name}'. " +
                    $"It holds '{before}' now. A PROFINET name is a DNS label: lowercase letters, " +
                    "digits and hyphens only.",
                    null,
                    failure);
            }
        }

        private static bool HasAttribute(Node node, string attributeName)
        {
            return node.GetAttributeInfos().Any(
                information => string.Equals(information.Name, attributeName, StringComparison.Ordinal));
        }
    }
}
