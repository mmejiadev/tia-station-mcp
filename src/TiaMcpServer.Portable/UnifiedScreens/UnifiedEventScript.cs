namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>The JavaScript an item runs on one of its events.</summary>
    /// <remarks>
    /// A WinCC Unified event is one script (measured 2026-10-23 on TIA Portal V20: a second handler for
    /// the same event is refused, "EventType Already has 'Action'"), so every function written for an
    /// event ends up in this one script.
    /// </remarks>
    public sealed class UnifiedEventScript
    {
        /// <summary>Creates an event script.</summary>
        /// <param name="eventName">The event as Openness names it on the item's type, e.g. <c>Down</c>, <c>Tapped</c>; any case.</param>
        /// <param name="script">The JavaScript.</param>
        /// <param name="globalDefinitions">The script's global definition area, or null to leave TIA Portal's own.</param>
        public UnifiedEventScript(string eventName, string script, string? globalDefinitions)
        {
            EventName = eventName;
            Script = script;
            GlobalDefinitions = globalDefinitions;
        }

        /// <summary>The event.</summary>
        public string EventName { get; }

        /// <summary>The JavaScript.</summary>
        public string Script { get; }

        /// <summary>The global definition area, or null to leave TIA Portal's own.</summary>
        public string? GlobalDefinitions { get; }
    }
}
