using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Online;
using Siemens.Engineering.SW;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Whether TIA Portal is online with a CPU, asked before an export or a write.
    ///
    /// Online, TIA Portal refuses every export and import with "This function is not supported in
    /// online mode" (measured 2026-09-30 on an S7-1200 connected to PLCSIM), a message that names
    /// neither the fix nor, in another UI language, anything a caller could match on. Asking
    /// Openness for the state instead of reading the message keeps the answer the same in every
    /// language. Reading the program works online and is not checked.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>The software a write or an export targets, refused while TIA Portal is online with its CPU.</summary>
        private PlcSoftware RequireOfflineSoftware(string softwarePath)
        {
            var software = RequireSoftware(softwarePath);

            if (IsInOnlineMode(software))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"TIA Portal is online with the CPU of {softwarePath}, and exports and imports only work offline. " +
                    "Go offline in TIA Portal (the Go offline button) and try again.");
            }

            return software;
        }

        private static bool IsInOnlineMode(PlcSoftware software)
        {
            var cpu = (software.Parent as SoftwareContainer)?.Parent as DeviceItem;
            var state = cpu?.GetService<OnlineProvider>()?.State;

            // Every state but these three means an online connection exists or is being opened or
            // closed; only the online one is measured, and the others are refused with it rather
            // than guessed to be harmless.
            return state != null
                && state != OnlineState.Offline
                && state != OnlineState.NotReachable
                && state != OnlineState.Incompatible;
        }
    }
}
