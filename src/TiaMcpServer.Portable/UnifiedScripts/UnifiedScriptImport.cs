namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>One name a script's <c>import</c> gives it from a global script module.</summary>
    public sealed class UnifiedScriptImport
    {
        /// <summary>Creates an import.</summary>
        /// <param name="alias">The name the script uses.</param>
        /// <param name="module">The module imported from.</param>
        /// <param name="function">The function imported by name; null when the whole module is (<c>* as alias</c>).</param>
        /// <param name="line">The line of the import.</param>
        public UnifiedScriptImport(string alias, string module, string? function, int line)
        {
            Alias = alias;
            Module = module;
            Function = function;
            Line = line;
        }

        /// <summary>The name the script uses.</summary>
        public string Alias { get; }

        /// <summary>The module imported from.</summary>
        public string Module { get; }

        /// <summary>The function imported by name; null when the whole module is.</summary>
        public string? Function { get; }

        /// <summary>The line of the import.</summary>
        public int Line { get; }
    }
}
