using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Multiuser;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The adapter over the Openness API, and the only place in the repository that talks to TIA
    /// Portal. It is one class across many files, and the split is by responsibility rather than by
    /// size: this file holds the connection itself -- attaching to a portal or starting one, the
    /// state it is in, and disposing of it -- and each PortalXxx.cs beside it holds one area.
    ///
    /// <list type="bullet">
    /// <item>PortalProject, PortalSession -- what is open</item>
    /// <item>PortalDevices, PortalSoftware -- what the project contains</item>
    /// <item>PortalBlocks, PortalTypes, PortalDocuments, PortalSourceCode -- the program</item>
    /// <item>PortalSimulation, PortalNetwork, PortalOpcUa -- deployment and configuration</item>
    /// <item>PortalPathLookup, PortalSoftwareContainer, PortalRecursiveWalks -- addressing</item>
    /// <item>PortalProjectTree, PortalSoftwareTree -- rendering</item>
    /// </list>
    ///
    /// It was one 3,770-line file until 2026-09-05, against this repository's own limit of 300.
    /// Splitting it waited on audit finding F2: while Openness types still crossed into the MCP
    /// layer, moving methods around would have been rearranging the same tangle. With the portal
    /// layer returning DTOs, this became a move of whole methods that the test suite can check.
    ///
    /// A partial class rather than collaborating classes, deliberately. Every one of these files
    /// works on the same three fields -- the portal, the project and the session -- and their
    /// lifetime is the reason this class exists at all: splitting that state across objects would
    /// multiply the places that can leave a zombie TIA Portal holding the licence.
    /// </remarks>
    public partial class Portal : IDisposable
    {
        // closing parantheses for regex characters ommitted, because they are not relevant for regex detection
        private readonly char[] _regexChars = ['.', '^', '$', '*', '+', '?', '(', '[', '{', '\\', '|'];

        private TiaPortal? _portal;
        private ProjectBase? _project;
        private LocalSession? _session;

        // True only when this instance started the TIA Portal process. It decides whether we may
        // close the open project on the way out: a portal we merely attached to belongs to the
        // user, and closing their project would be destructive. A portal we started is ours, and
        // closing the project is what actually releases the file handles on its directory.
        private bool _ownsPortalProcess;
        private bool _isDisposed;
        private readonly ILogger<Portal>? _logger;

        #region ctor

        public Portal(ILogger<Portal>? logger = null)
        {
            _logger = logger;
        }

        #endregion

        #region helper for mcp server

        public bool ProjectIsValid
        {
            get
            {
                if (_project == null)
                {
                    return false;
                }

                // Check if the project is a valid Project instance
                if ((_session == null) && (_project is Project))
                {
                    return true;
                }

                // If it's a MultiuserProject, we can also check its validity
                if ((_session != null) && (_project is MultiuserProject))
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsLocalSession
        {
            get
            {
                return _session != null;
            }
        }

        public bool IsLocalProject
        {
            get
            {
                return _session == null;
            }
        }

        #endregion

        #region helper for unit tests

        /// <summary>
        /// How many TIA Portal processes are already running.
        /// </summary>
        /// <remarks>
        /// Worth asking before doing anything long. <see cref="ConnectPortal"/> attaches to a
        /// running portal rather than starting its own, so an automated run launched while someone
        /// has TIA Portal open shares that session — including its open project and any dialog
        /// waiting for a human. A download started that way blocked for thirteen hours here.
        /// </remarks>
        /// <returns>The number of running TIA Portal processes.</returns>
        public static int GetRunningPortalCount()
        {
            return TiaPortal.GetProcesses().Count;
        }

        public static bool IsLocalSessionFile(string sessionPath)
        {
            // Check if the path ends with '.als\d+' using regex
            var regex = new Regex(@"\.als\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(sessionPath);
        }

        public static bool IsLocalProjectFile(string projectPath)
        {
            // Check if the path ends with '.ap\d+' using regex
            var regex = new Regex(@"\.ap\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(projectPath);
        }

        /// <summary>
        /// Closes the open project and releases TIA Portal. Never throws: a failure here must not
        /// mask the exception that caused the caller to bail out. Failures are logged instead.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the project and the TIA Portal connection.
        /// </summary>
        /// <param name="disposing">
        /// True when called from <see cref="Dispose()"/>. There is nothing to release from a
        /// finalizer: the TIA Portal handles are managed objects owned by the runtime.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _isDisposed)
            {
                return;
            }

            ReleaseProjectIfOwned();

            try
            {
                _portal?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to release TIA Portal; the process may stay alive and hold the licence");
            }

            _portal = null;
            _ownsPortalProcess = false;
            _isDisposed = true;
        }

        /// <summary>
        /// Closes the open project and session, but only when this instance started TIA Portal.
        /// </summary>
        /// <remarks>
        /// Closing matters for more than tidiness: until the project is closed, TIA Portal keeps
        /// file handles open inside the project directory, so deleting or moving that directory
        /// fails with a sharing violation. Disposing the portal alone does not release them in
        /// time. When we are only attached to someone else's portal, we leave their project alone.
        /// Never throws, so it is safe to call from Dispose.
        /// </remarks>
        private void ReleaseProjectIfOwned()
        {
            if (!_ownsPortalProcess)
            {
                _project = null;
                _session = null;

                return;
            }

            try
            {
                (_project as Project)?.Close();
                _session?.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close the project while releasing the portal");
            }

            _project = null;
            _session = null;
        }

        #endregion

        #region portal

        /// <summary>
        /// Connects to TIA Portal: attaches to a running instance if there is one, otherwise
        /// starts a new one.
        /// </summary>
        /// <param name="withUserInterface">
        /// Whether a newly started TIA Portal shows its user interface. Defaults to false, which
        /// starts much faster and does not steal the focus — the right default for a server
        /// running in the background. Ignored when attaching to an already running instance.
        /// </param>
        /// <returns>Always true; failures are reported by throwing.</returns>
        /// <exception cref="PortalException">The connection could not be established.</exception>
        public bool ConnectPortal(bool withUserInterface = false)
        {
            _logger?.LogInformation("Connecting to TIA Portal (withUserInterface={WithUserInterface})...", withUserInterface);

            try
            {
                _project = null;
                _session = null;
                _portal = null;
                _ownsPortalProcess = false;

                // Attaching does not take ownership of the process: Dispose() will only detach
                // from it, leaving TIA Portal running for whoever started it.
                var processes = TiaPortal.GetProcesses();
                if (processes.Any())
                {
                    _portal = processes.First().Attach();
                    AttachToFirstOpenProject();

                    return true;
                }

                var mode = withUserInterface ? TiaPortalMode.WithUserInterface : TiaPortalMode.WithoutUserInterface;

                // We started it, so this instance owns the process: whoever holds this Portal
                // must Dispose() it or TIA Portal is left running and holding the licence.
                _portal = new TiaPortal(mode);
                _ownsPortalProcess = true;

                return true;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ConnectFailed, $"Failed to connect to TIA Portal: {ex.Message}", null, ex);

                pex.Data["withUserInterface"] = withUserInterface;

                _logger?.LogError(pex, "ConnectPortal failed (withUserInterface={WithUserInterface})", withUserInterface);
                throw pex;
            }
        }

        private void AttachToFirstOpenProject()
        {
            if (_portal == null)
            {
                return;
            }

            if (_portal.LocalSessions.Any())
            {
                _session = _portal.LocalSessions.First();
                _project = _session.Project;
                return;
            }

            if (_portal.Projects.Any())
            {
                _project = _portal.Projects.First();
            }
        }

        public bool IsConnected()
        {
            return _portal != null;
        }

        /// <summary>
        /// Releases the TIA Portal connection. If this instance started TIA Portal, the process
        /// is closed; if it only attached to a running one, this merely detaches from it.
        /// </summary>
        /// <returns>Always true; failures are reported by throwing.</returns>
        /// <exception cref="PortalException">The portal could not be released.</exception>
        public bool DisconnectPortal()
        {
            _logger?.LogInformation("Disconnecting from TIA Portal...");

            try
            {
                ReleaseProjectIfOwned();

                _portal?.Dispose();
                _portal = null;
                _ownsPortalProcess = false;

                return true;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ConnectFailed, $"Failed to disconnect from TIA Portal: {ex.Message}", null, ex);

                _logger?.LogError(pex, "DisconnectPortal failed");
                throw pex;
            }
        }

        #endregion

        #region status

        public State GetState()
        {
            _logger?.LogInformation("Getting TIA Portal state...");
            if (_portal != null)
            {
                // check for existing local sessions
                if (_portal.LocalSessions.Any())
                {
                    _session = _portal.LocalSessions.First();
                    _project = _session.Project;
                }
                // checks for existing projects
                else if (_portal.Projects.Any())
                {
                    _project = _portal.Projects.First();
                }
            }

            return new State
            {
                IsConnected = IsConnected(),
                Project = _project != null ? _project.Name : "-",
                Session = _session != null ? _session.Project.Name : "-"
            };
        }

        #endregion

        #region private helper

        private bool IsPortalNull()
        {
            if (_portal == null)
            {
                _logger?.LogWarning("No TIA portal available.");

                return true;
            }

            return false;
        }

        private bool IsProjectNull()
        {
            if (_project == null)
            {
                _logger?.LogWarning("No TIA project available.");

                return true;
            }

            return false;
        }

        private bool IsSessionNull()
        {
            if (_session == null)
            {
                _logger?.LogWarning("No TIA session available.");

                return true;
            }

            return false;
        }

        #endregion

    }


}
