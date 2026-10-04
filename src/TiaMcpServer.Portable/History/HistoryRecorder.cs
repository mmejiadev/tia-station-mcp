using System;
using TiaMcpServer.Governance;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.History
{
    /// <summary>
    /// Records what happened to a project — its compilations, and the projects the server opened —
    /// for the web platform to import.
    /// </summary>
    /// <remarks>
    /// **A failed record never fails the operation it records.** The compilation already happened,
    /// and reporting it as a failure would tell the caller to compile again, which changes nothing
    /// and fails the same way. So a failure comes back as a sentence the tool puts in its answer —
    /// visible to the caller and to the log, never swallowed.
    ///
    /// The opposite of the audit trail, on purpose: a write that cannot be audited is refused in
    /// Workshop Mode, because the audit is a precondition of the change. The history is a record of
    /// it, and a gap in it is a gap, not a hazard.
    /// </remarks>
    public sealed class HistoryRecorder
    {
        private readonly IJournal<CompilationRecord> _compilations;
        private readonly IJournal<ProjectRecord> _projects;
        private readonly ISystemClock _clock;

        /// <summary>Creates a recorder.</summary>
        /// <param name="compilations">Where compilations are recorded.</param>
        /// <param name="projects">Where projects are recorded.</param>
        /// <param name="clock">Where the time of each record comes from.</param>
        public HistoryRecorder(IJournal<CompilationRecord> compilations, IJournal<ProjectRecord> projects, ISystemClock clock)
        {
            _compilations = compilations ?? throw new ArgumentNullException(nameof(compilations));
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>Records a compilation.</summary>
        /// <param name="projectPath">The project file.</param>
        /// <param name="softwarePath">The PLC software compiled.</param>
        /// <param name="report">What the compiler said.</param>
        /// <returns>Empty when it was recorded; otherwise a sentence saying it was not, and why.</returns>
        public string RecordCompilation(string projectPath, string softwarePath, CompilationReport report)
        {
            return TryAppend(
                () => _compilations.Append(new CompilationRecord(_clock.UtcNow, projectPath, softwarePath, report)),
                "compilation");
        }

        /// <summary>Records a project the server started working on.</summary>
        /// <param name="projectEvent">How: opened, retrieved or created.</param>
        /// <param name="project">What TIA Portal says about it.</param>
        /// <returns>Empty when it was recorded; otherwise a sentence saying it was not, and why.</returns>
        public string RecordProject(ProjectEvent projectEvent, ProjectSummary project)
        {
            return TryAppend(() => _projects.Append(new ProjectRecord(_clock.UtcNow, projectEvent, project)), "project");
        }

        private static string TryAppend(Action append, string what)
        {
            try
            {
                append();

                return string.Empty;
            }
            catch (PortalException failure)
            {
                return $"The {what} was not recorded in the history the web platform reads: {failure.Message}";
            }
        }
    }
}
