namespace TiaMcpServer.Scl
{
    /// <summary>A fault found in an SCL source.</summary>
    public sealed class SclFinding
    {
        /// <summary>Creates a finding.</summary>
        /// <param name="rule">The rule, e.g. <c>peek-poke-optimized-db</c>.</param>
        /// <param name="isError">Whether the code is wrong, rather than possibly not what was meant.</param>
        /// <param name="line">The source line.</param>
        /// <param name="message">What is wrong, and what to write instead.</param>
        public SclFinding(string rule, bool isError, int line, string message)
        {
            Rule = rule;
            IsError = isError;
            Line = line;
            Message = message;
        }

        /// <summary>The rule.</summary>
        public string Rule { get; }

        /// <summary>Whether the code is wrong, rather than possibly not what was meant.</summary>
        public bool IsError { get; }

        /// <summary>The source line.</summary>
        public int Line { get; }

        /// <summary>What is wrong, and what to write instead.</summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"line {Line}: {(IsError ? "error" : "warning")} {Rule}: {Message}";
        }
    }
}
