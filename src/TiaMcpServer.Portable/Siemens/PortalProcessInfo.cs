namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One running TIA Portal process, as much of it as choosing between processes needs.
    /// </summary>
    /// <remarks>
    /// Read from Openness's <c>TiaPortalProcess</c> by the adapter, so the choice itself can be
    /// tested without TIA Portal. That choice decides whose project the server writes into.
    /// </remarks>
    public sealed class PortalProcessInfo
    {
        /// <summary>Describes one process.</summary>
        /// <param name="id">The Windows process id.</param>
        /// <param name="hasUserInterface">Whether it shows a window a person works in.</param>
        /// <param name="projectPath">The open project's file path, or empty when none is open.</param>
        public PortalProcessInfo(int id, bool hasUserInterface, string projectPath)
        {
            Id = id;
            HasUserInterface = hasUserInterface;
            ProjectPath = projectPath ?? string.Empty;
        }

        /// <summary>The Windows process id.</summary>
        public int Id { get; }

        /// <summary>Whether it shows a window a person works in.</summary>
        public bool HasUserInterface { get; }

        /// <summary>The open project's file path, or empty when none is open.</summary>
        public string ProjectPath { get; }

        /// <summary>Whether a project is open in it.</summary>
        public bool HasProject => ProjectPath.Length > 0;

        /// <summary>The process in a few words, for a message to the caller.</summary>
        /// <returns>For example <c>TIA Portal process 1234 (with user interface) holding 'C:\x.ap20'</c>.</returns>
        public override string ToString()
        {
            var kind = HasUserInterface ? "with user interface" : "without user interface";
            var holding = HasProject ? $" holding '{ProjectPath}'" : " with no project open";

            return $"TIA Portal process {Id} ({kind}){holding}";
        }
    }
}
