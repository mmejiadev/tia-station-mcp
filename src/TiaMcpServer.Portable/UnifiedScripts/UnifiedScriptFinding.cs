namespace TiaMcpServer.UnifiedScripts
{
    /// <summary>One fault found in a WinCC Unified script.</summary>
    public sealed class UnifiedScriptFinding
    {
        /// <summary>Creates a finding.</summary>
        /// <param name="rule">The rule, e.g. <c>assignment-in-condition</c>.</param>
        /// <param name="severity">How sure it is.</param>
        /// <param name="line">The script line, from 1.</param>
        /// <param name="message">What is wrong and how to write it instead.</param>
        public UnifiedScriptFinding(string rule, UnifiedScriptSeverity severity, int line, string message)
        {
            Rule = rule;
            Severity = severity;
            Line = line;
            Message = message;
        }

        /// <summary>The rule.</summary>
        public string Rule { get; }

        /// <summary>How sure it is.</summary>
        public UnifiedScriptSeverity Severity { get; }

        /// <summary>The script line.</summary>
        public int Line { get; }

        /// <summary>What is wrong and how to write it instead.</summary>
        public string Message { get; }
    }
}
