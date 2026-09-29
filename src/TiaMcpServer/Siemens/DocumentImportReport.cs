using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The outcome of importing several SIMATIC SD documents: what reached the project, and which
    /// documents did not.
    /// </summary>
    /// <remarks>
    /// A bulk import does not stop at the first document that fails, so its outcome has two halves.
    /// Until 2026-09-28 the second was thrown away by two empty catches, and the tool answered
    /// "completed" with fewer blocks. On a write that is worse than on an export: the caller goes on
    /// to compile and download believing a block is in the project that is not.
    /// </remarks>
    public sealed class DocumentImportReport
    {
        /// <summary>Creates the report.</summary>
        /// <param name="imported">The blocks TIA Portal reports as imported.</param>
        /// <param name="failures">One line per document that was not imported, each starting with its name.</param>
        public DocumentImportReport(IReadOnlyList<BlockDescription> imported, IReadOnlyList<string> failures)
        {
            Imported = imported;
            Failures = failures;
        }

        /// <summary>The blocks TIA Portal reports as imported.</summary>
        public IReadOnlyList<BlockDescription> Imported { get; }

        /// <summary>
        /// One line per document that did not import cleanly, as <c>DocumentName: reason</c>. Its block
        /// is usually missing from <see cref="Imported"/>; after a partial success it can be in both.
        /// </summary>
        public IReadOnlyList<string> Failures { get; }
    }
}
