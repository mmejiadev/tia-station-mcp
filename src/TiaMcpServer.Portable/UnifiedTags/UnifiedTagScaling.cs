namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The linear scaling of a linked WinCC Unified tag: a PLC range mapped onto an HMI range.</summary>
    /// <remarks>
    /// Measured 2026-10-25: only a linked numeric tag has it, the four values are numbers (Openness
    /// refuses text), and the compile refuses a range whose start equals its end.
    /// </remarks>
    public sealed class UnifiedTagScaling
    {
        /// <summary>Creates a scaling.</summary>
        /// <param name="plc">The PLC range, start and end, e.g. 0 and 27648.</param>
        /// <param name="hmi">The HMI range, start and end, e.g. 0 and 100.</param>
        public UnifiedTagScaling((double Start, double End) plc, (double Start, double End) hmi)
        {
            Plc = plc;
            Hmi = hmi;
        }

        /// <summary>The PLC range.</summary>
        public (double Start, double End) Plc { get; }

        /// <summary>The HMI range.</summary>
        public (double Start, double End) Hmi { get; }
    }
}
