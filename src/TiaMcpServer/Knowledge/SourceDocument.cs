using System;

namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// The document a citation came from, identified well enough to be found again.
    /// </summary>
    /// <remarks>
    /// The version is not decoration. A manual is revised, and an excerpt from software release
    /// 5.16 may be wrong for 5.9; a citation that named only the title would look equally
    /// authoritative either way. It is carried through from the corpus recipe, which is the file
    /// that pins what was indexed.
    /// </remarks>
    public sealed class SourceDocument
    {
        /// <summary>Identifies a document.</summary>
        /// <param name="device">The equipment it documents, for example <c>UR5e</c>.</param>
        /// <param name="title">The document's own title.</param>
        /// <param name="version">The revision, as the recipe pinned it.</param>
        /// <exception cref="ArgumentException"><paramref name="device"/> or <paramref name="title"/> is empty.</exception>
        public SourceDocument(string device, string title, string version)
        {
            if (string.IsNullOrWhiteSpace(device))
            {
                throw new ArgumentException("A source document names the equipment it documents", nameof(device));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A source document needs its own title", nameof(title));
            }

            Device = device;
            Title = title;
            Version = version ?? string.Empty;
        }

        /// <summary>The equipment this document covers.</summary>
        public string Device { get; }

        /// <summary>The document's own title.</summary>
        public string Title { get; }

        /// <summary>The revision that was indexed, or empty when the recipe pinned none.</summary>
        public string Version { get; }

        /// <summary>Names the document and its revision.</summary>
        /// <returns>The description.</returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Version) ? Title : $"{Title} ({Version})";
        }
    }
}
