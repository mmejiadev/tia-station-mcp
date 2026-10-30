namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>When a WinCC Unified logging tag writes a value to its data log.</summary>
    public enum UnifiedLoggingMode
    {
        /// <summary>Every time the tag's value changes; TIA Portal's default.</summary>
        OnChange,

        /// <summary>Every cycle, which then has to be named: without one the compile refuses it.</summary>
        Cyclic,

        /// <summary>Only when asked to, by a script or a system function.</summary>
        OnDemand
    }
}
