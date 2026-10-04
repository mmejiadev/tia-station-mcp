namespace TiaMcpServer.Siemens
{
    /// <summary>How serious a compiler message is.</summary>
    public enum CompilationSeverity
    {
        /// <summary>The item compiled cleanly.</summary>
        Success,

        /// <summary>Informational only.</summary>
        Information,

        /// <summary>Compiles, but something is questionable.</summary>
        Warning,

        /// <summary>Does not compile.</summary>
        Error
    }
}
