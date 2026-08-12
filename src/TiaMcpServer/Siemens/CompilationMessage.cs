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

    /// <summary>
    /// One message from the compiler, flattened out of the nested tree TIA Portal returns.
    /// </summary>
    public sealed class CompilationMessage
    {
        /// <summary>Creates a compiler message.</summary>
        /// <param name="severity">How serious the message is.</param>
        /// <param name="path">Where it applies, as a project path.</param>
        /// <param name="description">What the compiler said.</param>
        public CompilationMessage(CompilationSeverity severity, string path, string description)
        {
            Severity = severity;
            Path = path;
            Description = description;
        }

        /// <summary>How serious the message is.</summary>
        public CompilationSeverity Severity { get; }

        /// <summary>
        /// Where the message applies. TIA Portal reports these as a tree, so the path here is the
        /// chain of parent entries joined with <c>/</c> — without it, a bare description like
        /// "Unknown identifier" says nothing about which block to fix.
        /// </summary>
        public string Path { get; }

        /// <summary>What the compiler said.</summary>
        public string Description { get; }

        /// <summary>Renders the message as one readable line.</summary>
        /// <returns>The severity, path and description.</returns>
        public override string ToString()
        {
            return $"{Severity}: {Path} — {Description}";
        }
    }
}
