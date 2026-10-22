using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Decides whether a WinCC Unified HMI may be compiled through the station that holds it.
    /// </summary>
    /// <remarks>
    /// TIA Portal V20 compiles a Unified HMI only with its whole station (measured 2026-10-17), and
    /// that would also compile any PLC program in the station — marking its blocks consistent, so
    /// downloadable. The write policy is asked about the HMI's path only, so a station holding a PLC
    /// program is refused: compiling it would get a program ready for a controller without the policy
    /// ever being asked. Kept apart from Openness so the rule is tested without TIA Portal.
    /// </remarks>
    public static class UnifiedStationCompileRule
    {
        /// <summary>Refuses unless the station holds no PLC program.</summary>
        /// <param name="hmiName">The Unified HMI to compile.</param>
        /// <param name="stationName">The station that holds it.</param>
        /// <param name="plcPrograms">Every PLC program in that station.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when the station holds a PLC program.
        /// </exception>
        public static void RequireNoPlcPrograms(string hmiName, string stationName, IReadOnlyCollection<string> plcPrograms)
        {
            if (plcPrograms == null)
            {
                throw new ArgumentNullException(nameof(plcPrograms));
            }

            if (plcPrograms.Count == 0)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"WinCC Unified HMI '{hmiName}' only compiles with its whole station, and station '{stationName}' also holds "
                + $"PLC program(s) {string.Join(", ", plcPrograms.Select(name => $"'{name}'"))}, which the write policy has not been asked about. "
                + "Compile the station in TIA Portal instead.");
        }
    }
}
