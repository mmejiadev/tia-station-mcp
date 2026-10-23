using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Which HMI runtimes a PC station may be built with, and how TIA Portal wants them plugged.
    /// </summary>
    /// <remarks>
    /// Measured 2026-10-20 on TIA Portal V20. A PC station is not a device created from an order
    /// number: it is a rack, <c>System:Rack.PC</c>, and the HMI runtime is a module plugged into it.
    /// Plugging the runtime with any name fails with "The name could not be set", although
    /// <c>CanPlugNew</c> said yes, and with <c>null</c> it fails too; plugged with an empty name, TIA
    /// Portal names it <c>HMI_RT_1</c> itself, and
    /// that name is read-only afterwards. A failed attempt followed by a second plug once took the
    /// whole TIA Portal process down, which is why only the exact runtimes measured are accepted — one
    /// version each, not every version of the order number: anything else is refused before Openness
    /// sees it, and another version is added here once it has been measured. Kept apart from Openness
    /// so the rule is tested without TIA Portal.
    /// </remarks>
    public static class PcHmiRuntime
    {
        /// <summary>The type identifier of the PC station's rack.</summary>
        public const string PcStationTypeIdentifier = "System:Rack.PC";

        /// <summary>WinCC Unified PC RT V20, measured 2026-10-20.</summary>
        public const string UnifiedPc = "OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2";

        /// <summary>WinCC RT Advanced V17, measured 2026-10-20.</summary>
        public const string RuntimeAdvanced = "OrderNumber:6AV2 104-0xxxx-xxxx/17.0.0.0";

        /// <summary>The network card a PC station is built with: "IE general", firmware V8.2.</summary>
        /// <remarks>
        /// A PC station's rack holds no interface of its own, so without a card it cannot join a subnet
        /// (measured 2026-10-22). The class projects use this one, in slot 2 after the runtime, named
        /// <c>IE general_1</c> (<c>0967_1</c>); plugged with an empty name it gets exactly that name and a
        /// PROFINET node <c>X1</c> whose address starts at 192.168.0.1 — the S7-1200's default too, so the
        /// station's address is set once it is on the PLC's subnet.
        /// </remarks>
        public const string NetworkCard = "OrderNumber:IE_CP/SW V8.2 ...";

        private static readonly string[] MeasuredRuntimes = { UnifiedPc, RuntimeAdvanced };

        /// <summary>Refuses a runtime that has not been measured as plugging into a PC station.</summary>
        /// <param name="runtimeTypeIdentifier">The runtime, as <c>FindCatalogEntries</c> gives it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is blank or not a measured HMI runtime.</exception>
        public static void RequireMeasured(string runtimeTypeIdentifier)
        {
            if (string.IsNullOrWhiteSpace(runtimeTypeIdentifier))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "runtimeTypeIdentifier is required");
            }

            if (MeasuredRuntimes.Any(measured => string.Equals(measured, runtimeTypeIdentifier.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{runtimeTypeIdentifier}' is not an HMI runtime this server has measured on a PC station. "
                + $"Use WinCC Unified PC ('{UnifiedPc}') or WinCC RT Advanced ('{RuntimeAdvanced}').");
        }

        /// <summary>Refuses plugging, moving or copying a module into a PC station's rack by hand.</summary>
        /// <param name="containerTypeIdentifier">The type identifier of the rack about to be plugged into.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is a PC station's rack.</exception>
        /// <remarks>
        /// The generic plug path names the module and may retry, which are the two things that fail on a
        /// PC station and once took TIA Portal down. A PC station is built with <c>AddPcStation</c> only.
        /// </remarks>
        public static void RequireNotPcStationRack(string? containerTypeIdentifier)
        {
            if (!string.Equals(containerTypeIdentifier, PcStationTypeIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                "A PC station's rack is not plugged into by hand: build the station with AddPcStation, which "
                + "plugs the HMI runtime the one way TIA Portal accepts. Nothing was changed.");
        }
    }
}
