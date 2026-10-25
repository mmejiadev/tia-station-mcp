namespace TiaMcpServer.UnifiedTags
{
    /// <summary>Everything written to a WinCC Unified tag once it exists.</summary>
    public sealed class UnifiedTagSettings
    {
        /// <summary>No settings: TIA Portal's defaults.</summary>
        public static readonly UnifiedTagSettings None = new UnifiedTagSettings(UnifiedTagOptions.None, UnifiedTagLimits.None, UnifiedTagPlcReading.None);

        /// <summary>Creates the settings.</summary>
        /// <param name="options">Comment, start value, retention and text length.</param>
        /// <param name="limits">The lower and upper limits.</param>
        /// <param name="plcReading">A linked tag's acquisition, scaling and substitute value.</param>
        public UnifiedTagSettings(UnifiedTagOptions options, UnifiedTagLimits limits, UnifiedTagPlcReading plcReading)
        {
            Options = options;
            Limits = limits;
            PlcReading = plcReading;
        }

        /// <summary>Comment, start value, retention and text length.</summary>
        public UnifiedTagOptions Options { get; }

        /// <summary>The lower and upper limits.</summary>
        public UnifiedTagLimits Limits { get; }

        /// <summary>A linked tag's acquisition, scaling and substitute value.</summary>
        public UnifiedTagPlcReading PlcReading { get; }
    }
}
