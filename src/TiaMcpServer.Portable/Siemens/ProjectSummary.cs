using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What TIA Portal says about a project: where it is, and who made and last changed it.
    /// </summary>
    /// <remarks>
    /// The author is the link the web platform uses between a project and a person (see
    /// docs/WEB-PLATFORM.md). It is what TIA Portal recorded — usually the Windows account that
    /// created the project — and is shown as that until somebody confirms who it is.
    /// </remarks>
    public sealed class ProjectSummary
    {
        /// <summary>Creates a summary.</summary>
        /// <param name="path">The project file.</param>
        /// <param name="name">The project's name.</param>
        /// <param name="author">Who TIA Portal says created it.</param>
        /// <param name="createdAt">When it was created.</param>
        /// <param name="lastModified">When it was last saved.</param>
        /// <param name="lastModifiedBy">Who last saved it.</param>
        public ProjectSummary(
            string path,
            string name,
            string author,
            DateTimeOffset createdAt,
            DateTimeOffset lastModified,
            string lastModifiedBy)
        {
            Path = path ?? string.Empty;
            Name = name ?? string.Empty;
            Author = author ?? string.Empty;
            CreatedAt = createdAt;
            LastModified = lastModified;
            LastModifiedBy = lastModifiedBy ?? string.Empty;
        }

        /// <summary>The project file.</summary>
        public string Path { get; }

        /// <summary>The project's name.</summary>
        public string Name { get; }

        /// <summary>Who TIA Portal says created it.</summary>
        public string Author { get; }

        /// <summary>When it was created.</summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>When it was last saved.</summary>
        public DateTimeOffset LastModified { get; }

        /// <summary>Who last saved it.</summary>
        public string LastModifiedBy { get; }
    }
}
