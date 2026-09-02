using System;

namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// One verbatim excerpt from a manufacturer's document, with everything needed to go and read it.
    /// </summary>
    /// <remarks>
    /// **The excerpt is quoted, never composed.** This type carries text that a document contains;
    /// nothing in this layer writes a sentence about hardware. That is the knowledge layer's
    /// cardinal rule, and it is the reason this class has no method that summarises, shortens or
    /// rephrases what it holds.
    ///
    /// Immutable, because a citation shown for approval and then edited is not a citation.
    /// </remarks>
    public sealed class HardwareCitation
    {
        /// <summary>Records an excerpt.</summary>
        /// <param name="document">The document's title and version, as the index recorded them.</param>
        /// <param name="page">The one-based page the excerpt was taken from.</param>
        /// <param name="excerpt">The text, exactly as the document has it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="excerpt"/> is empty, or <paramref name="page"/> is not positive.</exception>
        public HardwareCitation(SourceDocument document, int page, string excerpt)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (page < 1)
            {
                throw new ArgumentException($"A citation needs the page it came from, not {page}", nameof(page));
            }

            if (string.IsNullOrWhiteSpace(excerpt))
            {
                throw new ArgumentException("A citation with no text cites nothing", nameof(excerpt));
            }

            Document = document;
            Page = page;
            Excerpt = excerpt;
        }

        /// <summary>Where the excerpt came from.</summary>
        public SourceDocument Document { get; }

        /// <summary>The one-based page, so a reader can jump to it.</summary>
        public int Page { get; }

        /// <summary>The text, exactly as the document has it.</summary>
        public string Excerpt { get; }

        /// <summary>Names the source, without the excerpt, for a one-line summary.</summary>
        /// <returns>The description.</returns>
        public override string ToString()
        {
            return $"{Document}, page {Page}";
        }
    }
}
