using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Which TIA Portal the server works in, when the user has her own open beside it. The choices
    /// themselves are made by <see cref="PortalProcessSelector"/> and <see cref="ProjectOpeningRule"/>,
    /// which are tested without TIA Portal; this file reads the processes and carries them out.
    /// </remarks>
    public partial class Portal
    {
        private PortalProcessInfo? _attachedProcess;

        /// <summary>The TIA Portal process the server is attached to, or null when not connected.</summary>
        /// <remarks>
        /// Its project is the current one when there is one, not the one open when the server
        /// attached: a message built from the earlier one said "with no project open" beside the
        /// project just opened. With none current, it is what the process held when attached.
        /// </remarks>
        public PortalProcessInfo? AttachedProcess => _attachedProcess == null
            ? null
            : new PortalProcessInfo(
                _attachedProcess.Id,
                _attachedProcess.HasUserInterface,
                OpenProjectPath.Length > 0 ? OpenProjectPath : _attachedProcess.ProjectPath);

        /// <summary>Why that process was chosen.</summary>
        public PortalProcessChoiceReason AttachedProcessReason { get; private set; } = PortalProcessChoiceReason.NoneRunning;

        /// <summary>Makes a project path full, refusing one Windows cannot read as a path.</summary>
        /// <param name="projectPath">The path as the caller wrote it, or empty.</param>
        /// <returns>The full path, or empty when none was given.</returns>
        /// <exception cref="PortalException">The path is malformed: a validation error, not a failure.</exception>
        /// <remarks>
        /// <c>Path.GetFullPath</c> throws on a character a path cannot hold, and that exception
        /// reached the caller as an operation failure, which says "retry" about a typo.
        /// </remarks>
        public static string FullProjectPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(projectPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Not a usable project path: '{projectPath}' ({ex.Message})", null, ex);
            }
        }

        /// <summary>Every running TIA Portal that answers when asked what it is.</summary>
        /// <remarks>
        /// One that is still starting or is stuck can throw when asked for its mode or project, and
        /// used to fail the whole Connect while the user's healthy window ran beside it. It is left
        /// out and logged instead: not attaching to a process that cannot describe itself is a
        /// refusal, not a permission, and the message says which process was chosen.
        /// </remarks>
        private List<(TiaPortalProcess Process, PortalProcessInfo Info)> ReadProcesses()
        {
            var described = new List<(TiaPortalProcess, PortalProcessInfo)>();

            foreach (var process in TiaPortal.GetProcesses())
            {
                try
                {
                    described.Add((process, Describe(process)));
                }
                catch (EngineeringException ex)
                {
                    _logger?.LogWarning(ex, "TIA Portal process {ProcessId} could not be read and is left out", process.Id);
                }
            }

            return described;
        }

        private static PortalProcessInfo Describe(TiaPortalProcess process)
        {
            return new PortalProcessInfo(
                process.Id,
                process.Mode == TiaPortalMode.WithUserInterface,
                process.ProjectPath?.FullName ?? string.Empty);
        }

        /// <summary>Keeps a TIA Portal the server started when Connect is called again.</summary>
        /// <returns>True when there was one to keep.</returns>
        /// <remarks>
        /// Releasing it would close its project without saving and shut it down, so a Connect meant
        /// as a refresh would lose the work of the session: the same harm CreateProject refuses to
        /// do. A requested project is made current if it is open there, and otherwise none is.
        /// </remarks>
        private bool KeepOwnPortal(string requestedProjectPath)
        {
            if (_portal == null || !OwnsPortalProcess)
            {
                return false;
            }

            if (requestedProjectPath.Length > 0)
            {
                AttachToOpenProject(requestedProjectPath);
            }

            AttachedProcessReason = PortalProcessChoiceReason.KeptOwnPortal;

            return true;
        }

        /// <summary>Lets go of the attached portal: closes it if the server started it, detaches otherwise.</summary>
        /// <remarks>
        /// Detaching is <c>TiaPortal.Dispose()</c> on an attached portal, and it leaves that TIA Portal
        /// running: measured 2026-10-27, the user's window outlived two server sessions attached to
        /// it. It is <c>TiaPortalProcess.Dispose()</c> that closes one; see <see cref="AttachToChosenProcess"/>.
        /// </remarks>
        private void ReleasePortal()
        {
            try
            {
                ReleaseProjectIfOwned();
                _portal?.Dispose();
            }
            finally
            {
                // Reset whatever Dispose did: a portal that threw while closing is no connection,
                // and keeping it would make every later Connect fail on it the same way.
                _portal = null;
                OwnsPortalProcess = false;
                _attachedProcess = null;
            }
        }

        /// <summary>Attaches to the process <see cref="PortalProcessSelector"/> chooses, if any runs.</summary>
        /// <returns>True when attached; false when none runs and one must be started.</returns>
        /// <remarks>
        /// **No <c>TiaPortalProcess</c> is ever disposed here, chosen or not: disposing one closes
        /// that TIA Portal.** Measured on V20 on 2026-10-27, against the one rule in CLAUDE.md that
        /// says every TIA object is disposed: a window opened by hand, listed by
        /// <c>GetProcesses()</c> and left alone was still running fifteen seconds later; the same
        /// window listed and disposed was gone, unsaved work with it. A first version of this file
        /// disposed the processes it did not choose, and closed the user's window twice that way.
        /// The handle is a process descriptor, not a licence, and the upstream code never
        /// disposed it either.
        /// </remarks>
        private bool AttachToChosenProcess(string requestedProjectPath)
        {
            var processes = ReadProcesses();
            var choice = PortalProcessSelector.Choose(processes.Select(entry => entry.Info).ToList(), requestedProjectPath);
            var chosen = processes.FirstOrDefault(entry => entry.Info.Id == choice.Process?.Id).Process;

            if (chosen == null)
            {
                return false;
            }

            _portal = chosen.Attach();
            _attachedProcess = choice.Process;
            AttachedProcessReason = choice.Reason;
            _logger?.LogInformation("Attached to {Process} ({Reason})", AttachedProcess, AttachedProcessReason);

            return true;
        }

        private void StartOwnPortal(bool withUserInterface)
        {
            var mode = withUserInterface ? TiaPortalMode.WithUserInterface : TiaPortalMode.WithoutUserInterface;

            // We started it, so this instance owns the process: whoever holds this Portal
            // must Dispose() it or TIA Portal is left running and holding the licence.
            _portal = new TiaPortal(mode);
            OwnsPortalProcess = true;
            _attachedProcess = new PortalProcessInfo(_portal.GetCurrentProcess().Id, withUserInterface, string.Empty);
            AttachedProcessReason = PortalProcessChoiceReason.NoneRunning;
            _logger?.LogInformation("Started {Process}", AttachedProcess);
        }

        /// <summary>Makes the requested project current, or with none requested, whatever is open.</summary>
        /// <remarks>
        /// **A requested project that is not open leaves no project current**, rather than whichever
        /// one the portal lists first: the caller asked for one project, and the next write it makes
        /// would otherwise land in another. The requested project is looked for before a session,
        /// so a session open beside it does not stand in for it either.
        /// </remarks>
        private void AttachToOpenProject(string requestedProjectPath)
        {
            _session = null;
            CurrentProject = null;

            if (_portal == null)
            {
                return;
            }

            if (requestedProjectPath.Length > 0)
            {
                CurrentProject = FindOpenProject(requestedProjectPath);
                return;
            }

            _session = _portal.LocalSessions.FirstOrDefault();
            CurrentProject = _session != null ? (ProjectBase)_session.Project : _portal.Projects.FirstOrDefault();
        }

        private Project? FindOpenProject(string projectPath)
        {
            return _portal?.Projects.FirstOrDefault(project => ProjectFilePath.AreSame(project.Path?.FullName ?? string.Empty, projectPath));
        }

        /// <summary>The project the attached portal holds: the requested one if open, else a session, else the first.</summary>
        private string ProjectPathInAttachedPortal(string requestedProjectPath)
        {
            var requested = FindOpenProject(requestedProjectPath);

            if (requested != null)
            {
                return requested.Path.FullName;
            }

            var session = _portal?.LocalSessions.FirstOrDefault();
            var project = session != null ? (ProjectBase)session.Project : _portal?.Projects.FirstOrDefault();

            return project?.Path?.FullName ?? string.Empty;
        }

        /// <summary>Whether the project the server works on is still open, or the user closed it by hand.</summary>
        private bool IsCurrentProjectStillOpen()
        {
            if (_portal == null || OpenProjectPath.Length == 0)
            {
                return false;
            }

            return _portal.Projects.Any(project => ProjectFilePath.AreSame(project.Path?.FullName ?? string.Empty, OpenProjectPath))
                || _portal.LocalSessions.Any(session => ProjectFilePath.AreSame(session.Project?.Path?.FullName ?? string.Empty, OpenProjectPath));
        }

        /// <remarks>Not disposed, for the reason in <see cref="AttachToChosenProcess"/>: it would close them.</remarks>
        private List<PortalProcessInfo> ReadOtherProcesses(int attachedProcessId)
        {
            return ReadProcesses()
                .Select(entry => entry.Info)
                .Where(process => process.Id != attachedProcessId)
                .ToList();
        }
    }
}
