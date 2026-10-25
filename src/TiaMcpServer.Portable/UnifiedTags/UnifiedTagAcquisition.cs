using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>How and how often a linked WinCC Unified tag is read from the PLC.</summary>
    /// <remarks>
    /// A cycle is a named object of the HMI, not a duration: Openness takes any name and the compile then
    /// says "The object 'T3s' does not exist". The names below are every one that compiled out of 24 tried
    /// on TIA Portal V20 (measured 2026-10-25); Openness V20 does not list them. A tag read on demand has no
    /// cycle: setting one is refused as a disabled field.
    /// </remarks>
    public sealed class UnifiedTagAcquisition
    {
        private static readonly string[] MeasuredCycles = { "T100ms", "T250ms", "T500ms", "T1s", "T2s", "T5s", "T10s" };

        /// <summary>Creates an acquisition.</summary>
        /// <param name="mode">How the tag is read.</param>
        /// <param name="cycle">The cycle, already normalised; null for on demand, or to keep TIA Portal's T1s.</param>
        public UnifiedTagAcquisition(UnifiedAcquisitionMode mode, string? cycle)
        {
            Mode = mode;
            Cycle = cycle;
        }

        /// <summary>Every cycle a tag takes.</summary>
        public static IReadOnlyList<string> Cycles => MeasuredCycles;

        /// <summary>How the tag is read.</summary>
        public UnifiedAcquisitionMode Mode { get; }

        /// <summary>The cycle, or null.</summary>
        public string? Cycle { get; }

        /// <summary>A cycle as TIA Portal spells it.</summary>
        /// <param name="cycle">The cycle, in any case, e.g. <c>t1s</c>.</param>
        /// <param name="what">The tag, as a refusal names it.</param>
        /// <returns>The measured spelling, e.g. <c>T1s</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> listing the cycles there are.</exception>
        public static string NormaliseCycle(string cycle, string what)
        {
            return MeasuredCycles.FirstOrDefault(known => known.Equals(cycle.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{cycle}' is not an acquisition cycle of the HMI. Use one of: {string.Join(", ", MeasuredCycles)}");
        }
    }
}
