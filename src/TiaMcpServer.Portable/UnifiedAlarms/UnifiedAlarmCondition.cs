namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>When a WinCC Unified analog alarm is raised, comparing its tag with its value.</summary>
    public enum UnifiedAlarmCondition
    {
        /// <summary>The tag is above the value.</summary>
        UpperLimit,

        /// <summary>The tag is above or at the value.</summary>
        UpperLimitOrEqual,

        /// <summary>The tag is below the value.</summary>
        LowerLimit,

        /// <summary>The tag is below or at the value.</summary>
        LowerLimitOrEqual,

        /// <summary>The tag equals the value.</summary>
        Equal,

        /// <summary>The tag differs from the value.</summary>
        NotEqual
    }
}
