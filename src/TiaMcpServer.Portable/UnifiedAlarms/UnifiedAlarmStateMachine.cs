namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>The states an alarm of a WinCC Unified alarm class goes through, as Openness V20 names them.</summary>
    public enum UnifiedAlarmStateMachine
    {
        /// <summary>Raised only: no clear, no acknowledgement.</summary>
        Raise,

        /// <summary>Raised and cleared, no acknowledgement.</summary>
        RaiseClear,

        /// <summary>Raised and acknowledged, no clear.</summary>
        RaiseRequiresAcknowledgement,

        /// <summary>Raised and cleared; acknowledging is allowed but not required.</summary>
        RaiseClearOptionalAcknowledgement,

        /// <summary>Raised, cleared and acknowledged; the system classes Alarm and Warning use it.</summary>
        RaiseClearRequiresAcknowledgement,

        /// <summary>As above, and reset after that.</summary>
        RaiseClearRequiresAcknowledgementAndReset
    }
}
