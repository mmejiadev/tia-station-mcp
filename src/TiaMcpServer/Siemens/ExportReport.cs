using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The outcome of a bulk export: what was written, and what went wrong on the way.
    /// </summary>
    /// <typeparam name="TItem">What was exported: a block or a type description.</typeparam>
    /// <remarks>
    /// A bulk export does not stop at the first item that fails, so its outcome has two halves. For
    /// the SimaticML exports the second reached only the log until 2026-09-30, and for the document
    /// export until 2026-09-27: a caller was told how many items were exported and had to count the
    /// missing ones itself, with nothing to say why they were missing.
    /// </remarks>
    public sealed class ExportReport<TItem>
    {
        /// <summary>Creates the report.</summary>
        /// <param name="exported">The items whose files were written.</param>
        /// <param name="failures">One line per problem met, each starting with the item's name.</param>
        public ExportReport(IReadOnlyList<TItem> exported, IReadOnlyList<string> failures)
        {
            Exported = exported;
            Failures = failures;
        }

        /// <summary>The items whose files were written.</summary>
        public IReadOnlyList<TItem> Exported { get; }

        /// <summary>
        /// One line per problem met, as <c>Name: reason</c>. An item is usually here because it is
        /// missing from <see cref="Exported"/>; a document can be in both when a stale one could not
        /// be removed but the new export overwrote it.
        /// </summary>
        public IReadOnlyList<string> Failures { get; }
    }
}
