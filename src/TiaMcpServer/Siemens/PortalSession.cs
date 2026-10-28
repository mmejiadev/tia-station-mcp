using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Multiuser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Multiuser sessions: opening a local session, saving it and closing it.
    /// </remarks>
    public partial class Portal
    {
        private List<ProjectBase> FindSessions()
        {
            _logger?.LogInformation("Getting open local sessions...");

            if (IsPortalNull())
            {
                return [];
            }

            var sessions = new List<ProjectBase>();

            if (_portal?.LocalSessions != null)
            {
                foreach (var session in _portal.LocalSessions)
                {
                    sessions.Add(session.Project as ProjectBase);
                }
            }

            return sessions;
        }

        /// <summary>
        /// Opens a local session (<c>.alsNN</c>), without closing anything open in a TIA Portal the
        /// server did not start.
        /// </summary>
        /// <param name="localSessionPath">The local session file.</param>
        /// <returns>True when the session is open and current; false when no portal is connected.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when somebody else's TIA Portal holds a project
        /// or session; an operation failure when TIA Portal could not open it.
        /// </exception>
        public bool OpenSession(string localSessionPath)
        {
            _logger?.LogInformation("Opening session: {SessionPath}", localSessionPath);

            if (IsPortalNull())
            {
                return false;
            }

            try
            {
                RefuseSessionBesideSomebodysWork(localSessionPath);

                var sessionName = SessionNameOf(localSessionPath);

                CloseOtherOwnSession(sessionName);

                _session = FindOpenSession(sessionName) ?? _portal!.LocalSessions.Open(new FileInfo(localSessionPath));
                CurrentProject = _session?.Project;

                return CurrentProject != null;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Failed to open session: {ex.Message}", null, ex);

                pex.Data["localSessionPath"] = localSessionPath;
                pex.Data["attachedProcess"] = AttachedProcess?.ToString();

                _logger?.LogError(pex, "OpenSession failed for {SessionPath} in {Process}", localSessionPath, AttachedProcess);
                throw pex;
            }
        }

        /// <remarks>
        /// <see cref="ProjectOpeningRule"/> decides, as for a project: anything open in a TIA Portal the
        /// server did not start, project or session, refuses. A session's file is not the path its
        /// project reports, so even the same session open there refuses rather than being guessed at;
        /// Connect already makes an open session current.
        /// </remarks>
        private void RefuseSessionBesideSomebodysWork(string localSessionPath)
        {
            var openPath = ProjectPathInAttachedPortal(string.Empty);
            var request = new ProjectOpeningRequest(FullProjectPath(localSessionPath), openPath, OwnsPortalProcess, new List<PortalProcessInfo>());

            if (ProjectOpeningRule.Decide(request).Action == ProjectOpeningAction.Refuse)
            {
                throw RefuseToCloseUsersProject(localSessionPath, openPath);
            }
        }

        private static string SessionNameOf(string localSessionPath)
        {
            var projectName = Path.GetFileNameWithoutExtension(localSessionPath);

            return Regex.Replace(projectName, @"_(LS|ES)_\d$", string.Empty, RegexOptions.IgnoreCase);
        }

        /// <remarks>
        /// Reached only for the server's own TIA Portal, or one with nothing open: the refusal above
        /// stops every other case. Sessions are matched by name, as Openness names them; within the
        /// server's own portal, those are sessions the server opened itself.
        /// </remarks>
        private void CloseOtherOwnSession(string sessionName)
        {
            if (_session == null || _session.Project.Name == sessionName)
            {
                return;
            }

            CurrentProject = null;
            _session.Close();
            _session = null;
        }

        private LocalSession? FindOpenSession(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                return null;
            }

            return _portal?.LocalSessions.FirstOrDefault(session => session.Project.Name == sessionName);
        }

        public bool SaveSession()
        {
            _logger?.LogInformation("Saving session...");

            if (IsSessionNull())
            {
                return false;
            }

            // Save session
            _session?.Save();

            return true;
        }

        public bool CloseSession()
        {
            _logger?.LogInformation("Closing session...");

            if (IsSessionNull())
            {
                return false;
            }

            CurrentProject = null;
            _session?.Close();
            _session = null;

            return true;
        }
    }
}
