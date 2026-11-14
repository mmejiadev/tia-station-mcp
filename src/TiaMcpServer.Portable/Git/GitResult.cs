namespace TiaMcpServer.Git
{
    /// <summary>What one Git command answered.</summary>
    public sealed class GitResult
    {
        /// <summary>Creates the result.</summary>
        /// <param name="exitCode">Git's exit code.</param>
        /// <param name="output">What it wrote to standard output.</param>
        /// <param name="error">What it wrote to standard error.</param>
        public GitResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        /// <summary>Git's exit code.</summary>
        public int ExitCode { get; }

        /// <summary>What it wrote to standard output.</summary>
        public string Output { get; }

        /// <summary>What it wrote to standard error.</summary>
        public string Error { get; }

        /// <summary>Whether it succeeded.</summary>
        public bool Succeeded => ExitCode == 0;
    }
}
