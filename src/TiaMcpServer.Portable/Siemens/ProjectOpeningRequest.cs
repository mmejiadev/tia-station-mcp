using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>Everything <see cref="ProjectOpeningRule"/> needs to decide how to open a project.</summary>
    public sealed class ProjectOpeningRequest
    {
        /// <summary>Describes the request and the state around it.</summary>
        /// <param name="requestedPath">The full path of the project to open.</param>
        /// <param name="openProjectPath">The project open in the attached TIA Portal, or empty.</param>
        /// <param name="ownsPortal">Whether the server started the attached TIA Portal itself.</param>
        /// <param name="otherProcesses">Every other running TIA Portal.</param>
        public ProjectOpeningRequest(
            string requestedPath,
            string openProjectPath,
            bool ownsPortal,
            IReadOnlyList<PortalProcessInfo> otherProcesses)
        {
            RequestedPath = requestedPath ?? string.Empty;
            OpenProjectPath = openProjectPath ?? string.Empty;
            OwnsPortal = ownsPortal;
            OtherProcesses = otherProcesses ?? new List<PortalProcessInfo>();
        }

        /// <summary>The full path of the project to open.</summary>
        public string RequestedPath { get; }

        /// <summary>The project open in the attached TIA Portal, or empty.</summary>
        public string OpenProjectPath { get; }

        /// <summary>Whether the server started the attached TIA Portal itself.</summary>
        public bool OwnsPortal { get; }

        /// <summary>Every other running TIA Portal.</summary>
        public IReadOnlyList<PortalProcessInfo> OtherProcesses { get; }
    }
}
