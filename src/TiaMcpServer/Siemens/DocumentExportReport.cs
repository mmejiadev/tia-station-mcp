using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The outcome of exporting several blocks as SIMATIC SD documents: what was written, and
    /// what went wrong on the way.
    /// </summary>
    /// <remarks>
    /// A bulk export does not stop at the first block that fails, so its outcome has two halves.
    /// The second used to reach only the log: a caller was told how many blocks were exported and
    /// had to count the missing ones itself, with nothing to say why they were missing.
    /// </remarks>
    public sealed class DocumentExportReport
    {
        /// <summary>Creates the report.</summary>
        /// <param name="exported">The blocks whose documents were written.</param>
        /// <param name="failures">One line per problem met, each starting with the block's name.</param>
        public DocumentExportReport(IReadOnlyList<BlockDescription> exported, IReadOnlyList<string> failures)
        {
            Exported = exported;
            Failures = failures;
        }

        /// <summary>The blocks whose documents were written, consistent or not.</summary>
        public IReadOnlyList<BlockDescription> Exported { get; }

        /// <summary>
        /// One line per problem met, as <c>BlockName: reason</c>. A block is usually here because it
        /// is missing from <see cref="Exported"/>; it can be in both when a stale document could not
        /// be removed but the new export overwrote it.
        /// </summary>
        public IReadOnlyList<string> Failures { get; }
    }
}
