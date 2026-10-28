namespace TiaMcpServer.UnifiedTags
{
    /// <summary>How a linked WinCC Unified tag reads the PLC: acquisition, linear scaling and substitute value.</summary>
    /// <remarks>All three are disabled fields on an internal tag (measured 2026-10-25), so only a linked tag takes them.</remarks>
    public sealed class UnifiedTagPlcReading
    {
        /// <summary>TIA Portal's defaults.</summary>
        public static readonly UnifiedTagPlcReading None = new UnifiedTagPlcReading(null, null, null);

        /// <summary>Creates the settings; null leaves TIA Portal's default.</summary>
        /// <param name="acquisition">How and how often the tag is read.</param>
        /// <param name="scaling">The linear scaling.</param>
        /// <param name="substitute">The substitute value.</param>
        public UnifiedTagPlcReading(UnifiedTagAcquisition? acquisition, UnifiedTagScaling? scaling, UnifiedTagSubstitute? substitute)
        {
            Acquisition = acquisition;
            Scaling = scaling;
            Substitute = substitute;
        }

        /// <summary>How and how often the tag is read, or null.</summary>
        public UnifiedTagAcquisition? Acquisition { get; }

        /// <summary>The linear scaling, or null.</summary>
        public UnifiedTagScaling? Scaling { get; }

        /// <summary>The substitute value, or null.</summary>
        public UnifiedTagSubstitute? Substitute { get; }

        /// <summary>Whether any of the three is set.</summary>
        public bool IsSet => Acquisition != null || Scaling != null || Substitute != null;
    }
}
