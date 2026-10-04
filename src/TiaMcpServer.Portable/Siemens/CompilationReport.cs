using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The outcome of compiling a PLC software, as data rather than as an Openness object.
    /// </summary>
    /// <remarks>
    /// TIA Portal returns a <c>CompilerResult</c> whose messages form a tree, and whose
    /// <c>ToString()</c> yields the type name. Handing that straight to the caller meant a failed
    /// compile reported nothing usable about what went wrong — which defeats the point of
    /// compiling from an agent at all. This type is the flattened, actionable form.
    /// </remarks>
    public sealed class CompilationReport
    {
        /// <summary>Creates a compilation report.</summary>
        /// <param name="severity">The overall result state.</param>
        /// <param name="errorCount">Errors reported by TIA Portal.</param>
        /// <param name="warningCount">Warnings reported by TIA Portal.</param>
        /// <param name="messages">Every message, flattened, in the order the compiler produced them.</param>
        public CompilationReport(
            CompilationSeverity severity,
            int errorCount,
            int warningCount,
            IReadOnlyList<CompilationMessage> messages)
        {
            Severity = severity;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Messages = messages;
        }

        /// <summary>The overall result state.</summary>
        public CompilationSeverity Severity { get; }

        /// <summary>Errors reported by TIA Portal.</summary>
        public int ErrorCount { get; }

        /// <summary>Warnings reported by TIA Portal.</summary>
        public int WarningCount { get; }

        /// <summary>Every message, flattened, in the order the compiler produced them.</summary>
        public IReadOnlyList<CompilationMessage> Messages { get; }

        /// <summary>True when the software compiled without errors.</summary>
        public bool IsSuccessful => Severity != CompilationSeverity.Error && ErrorCount == 0;

        /// <summary>
        /// Only the messages that must be fixed. This is what a generate-compile-fix loop feeds
        /// back in.
        /// </summary>
        /// <remarks>
        /// Warnings and information are filtered out, and so are the messages with no description.
        /// TIA Portal marks every branch of the message tree with the worst severity found beneath
        /// it, so a single bad line produces an "Error" entry for the device, one for the program
        /// blocks folder and one for the block, none of which says anything. Keeping them would
        /// bury the two lines that actually name the problem.
        /// </remarks>
        public IReadOnlyList<CompilationMessage> Errors =>
            Messages
                .Where(message => message.Severity == CompilationSeverity.Error)
                .Where(message => !string.IsNullOrWhiteSpace(message.Description))
                .ToList();
    }
}
