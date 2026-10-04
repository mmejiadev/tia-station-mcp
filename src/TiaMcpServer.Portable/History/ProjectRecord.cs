using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.History
{
    /// <summary>
    /// A project the server started working on, as the project journal records it.
    /// </summary>
    public sealed class ProjectRecord
    {
        /// <summary>Records a project.</summary>
        /// <param name="timestamp">When the server started working on it.</param>
        /// <param name="projectEvent">How: opened, retrieved or created.</param>
        /// <param name="project">What TIA Portal says about it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="project"/> is null.</exception>
        public ProjectRecord(DateTimeOffset timestamp, ProjectEvent projectEvent, ProjectSummary project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            Timestamp = timestamp;
            Event = projectEvent;
            Path = project.Path;
            Name = project.Name;
            Author = project.Author;
            CreatedAt = project.CreatedAt;
            LastModified = project.LastModified;
            LastModifiedBy = project.LastModifiedBy;
        }

        /// <summary>When the server started working on it.</summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>How: opened, retrieved or created.</summary>
        public ProjectEvent Event { get; }

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
