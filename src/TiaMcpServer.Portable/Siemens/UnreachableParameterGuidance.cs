using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Says what to do about a parameter that is known to exist in TIA Portal but that Openness does
    /// not expose on every device.
    /// </summary>
    /// <remarks>
    /// The system and clock memory bytes are what class exercises lean on: <c>Clock_1Hz</c> counts
    /// seconds and <c>FirstScan</c> initialises. Siemens documents them as <c>SystemMemoryByte</c>,
    /// <c>ClockMemoryByte</c> and their addresses for the S7-1500 only, and on an S7-1200 TIA Portal
    /// V20 does not expose them at all: measured on 2026-10-03 on a CPU 1214C at firmware V4.7, they
    /// are missing from the attribute list and each throws "not supported" when asked for by name.
    ///
    /// Without this, asking for one on such a device is answered with "no parameter called" and a
    /// list of similar names that does not contain it — which reads as a misspelling, and sends
    /// somebody looking for a spelling that does not exist. The way out is by hand, so the refusal
    /// says where.
    /// </remarks>
    public static class UnreachableParameterGuidance
    {
        private const string SystemAndClockMemory =
            "TIA Portal does not expose the system and clock memory to Openness on every CPU: on an " +
            "S7-1200 (measured with TIA Portal V20) it cannot be set from here. Enable it by hand in " +
            "TIA Portal: the CPU's properties, General > System and clock memory. Once ticked, the tags " +
            "Clock_1Hz, FirstScan and the rest appear in the default tag table.";

        private static readonly Dictionary<string, string> GuidanceByName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SystemMemoryByte"] = SystemAndClockMemory,
                ["SystemMemoryByteAddress"] = SystemAndClockMemory,
                ["ClockMemoryByte"] = SystemAndClockMemory,
                ["ClockMemoryByteAddress"] = SystemAndClockMemory
            };

        /// <summary>Finds the guidance for a parameter a device did not expose.</summary>
        /// <param name="parameterName">The parameter as the caller wrote it; case is ignored.</param>
        /// <param name="guidance">What to do instead, or empty when nothing is known about it.</param>
        /// <returns>Whether there is guidance for that parameter.</returns>
        public static bool TryFind(string parameterName, out string guidance)
        {
            if (parameterName != null && GuidanceByName.TryGetValue(parameterName.Trim(), out var found))
            {
                guidance = found;
                return true;
            }

            guidance = string.Empty;
            return false;
        }
    }
}
