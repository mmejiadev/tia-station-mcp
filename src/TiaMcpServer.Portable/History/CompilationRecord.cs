using System;
using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.History
{
    /// <summary>
    /// One compilation of a PLC program, as the compilation journal records it.
    /// </summary>
    /// <remarks>
    /// Every message, not only the errors. The compile tool hands a loop the errors to fix; this is
    /// for a person reading a project's history, for whom a warning that appeared last week is the
    /// interesting part.
    /// </remarks>
    public sealed class CompilationRecord
    {
        /// <summary>Records a compilation.</summary>
        /// <param name="timestamp">When it finished.</param>
        /// <param name="projectPath">The project file, as TIA Portal reports its path.</param>
        /// <param name="softwarePath">The PLC software that was compiled.</param>
        /// <param name="report">What the compiler said.</param>
        /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
        public CompilationRecord(DateTimeOffset timestamp, string projectPath, string softwarePath, CompilationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            Timestamp = timestamp;
            ProjectPath = projectPath ?? string.Empty;
            SoftwarePath = softwarePath ?? string.Empty;
            Severity = report.Severity;
            ErrorCount = report.ErrorCount;
            WarningCount = report.WarningCount;
            Messages = report.Messages;
        }

        /// <summary>When the compilation finished.</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>The project file, as TIA Portal reports its path.</summary>
        public string ProjectPath { get; }

        /// <summary>The PLC software that was compiled.</summary>
        public string SoftwarePath { get; }

        /// <summary>The overall result.</summary>
        public CompilationSeverity Severity { get; }

        /// <summary>Errors reported by TIA Portal.</summary>
        public int ErrorCount { get; }

        /// <summary>Warnings reported by TIA Portal.</summary>
        public int WarningCount { get; }

        /// <summary>Every message, in the order the compiler produced them.</summary>
        public IReadOnlyList<CompilationMessage> Messages { get; }
    }
}
