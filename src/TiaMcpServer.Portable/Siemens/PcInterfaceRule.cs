using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Which PC network interfaces lead to a simulated controller.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-13 on TIA Portal V21: while any PLCSIM Advanced instance exists, even in STOP, TIA Portal
    /// offers a CPU one interface, <c>PLCSIM</c>; with none, it offered the PC's real adapters (Wi-Fi, VMware's) and
    /// "Siemens PLCSIM Virtual Ethernet Adapter". Both PLCSIM interfaces carry the word, which is what the download to
    /// PLCSIM has always gone by. Kept apart from Openness so the rule is tested without TIA Portal.
    /// </remarks>
    public static class PcInterfaceRule
    {
        /// <summary>The word every interface leading to PLCSIM has in its name.</summary>
        public const string SimulationMarker = "PLCSIM";

        /// <summary>Whether an interface leads to a simulated controller.</summary>
        /// <param name="pcInterfaceName">The interface, as TIA Portal names it.</param>
        /// <returns>True when its name carries <see cref="SimulationMarker"/>.</returns>
        public static bool IsSimulation(string? pcInterfaceName)
        {
            return (pcInterfaceName ?? string.Empty).IndexOf(SimulationMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
