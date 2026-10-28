namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>A state an alarm shows in, each with its own colours in its alarm class.</summary>
    public enum UnifiedAlarmState
    {
        /// <summary>Raised and not yet acknowledged or cleared.</summary>
        Raised,

        /// <summary>Acknowledged, still raised.</summary>
        Acknowledged,

        /// <summary>Cleared, not yet acknowledged.</summary>
        Cleared,

        /// <summary>Acknowledged and cleared.</summary>
        AcknowledgedCleared
    }
}
