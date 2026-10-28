namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>How sure a script finding is to be a fault.</summary>
    public enum UnifiedScriptSeverity
    {
        /// <summary>The script does not do what it reads as, or fails when it runs.</summary>
        Error,

        /// <summary>Very likely a mistake, worth a look.</summary>
        Warning
    }
}
