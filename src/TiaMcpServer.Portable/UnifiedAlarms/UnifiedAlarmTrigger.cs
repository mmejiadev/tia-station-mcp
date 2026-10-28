namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>Which edge of its bit raises a WinCC Unified discrete alarm.</summary>
    public enum UnifiedAlarmTrigger
    {
        /// <summary>The bit going to 1; TIA Portal's default.</summary>
        RisingEdge,

        /// <summary>The bit going to 0.</summary>
        FallingEdge
    }
}
