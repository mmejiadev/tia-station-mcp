using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Result of compiling a PLC software.</summary>
    public sealed class ResponseCompileSoftware : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="errorCount">Errors reported by the compiler.</param>
        /// <param name="warningCount">Warnings reported by the compiler.</param>
        /// <param name="messages">Every compiler message, one readable line each.</param>
        public ResponseCompileSoftware(int errorCount, int warningCount, IReadOnlyList<string> messages)
        {
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Messages = messages;
        }

        /// <summary>Errors reported by the compiler.</summary>
        public int ErrorCount { get; }

        /// <summary>Warnings reported by the compiler.</summary>
        public int WarningCount { get; }

        /// <summary>
        /// Every compiler message as <c>Severity: path — description</c>. This is what a
        /// generate, compile and fix loop reads to know what to change.
        /// </summary>
        public IReadOnlyList<string> Messages { get; }
    }
}
