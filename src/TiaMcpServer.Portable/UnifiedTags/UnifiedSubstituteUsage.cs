namespace TiaMcpServer.UnifiedTags
{
    /// <summary>When a linked WinCC Unified tag shows its substitute value instead of the PLC's.</summary>
    public enum UnifiedSubstituteUsage
    {
        /// <summary>When the value read is invalid, e.g. the connection is down.</summary>
        InvalidValue,

        /// <summary>When the value is outside the tag's limits.</summary>
        RangeViolation,

        /// <summary>In either case.</summary>
        InvalidValueOrRangeViolation
    }
}
