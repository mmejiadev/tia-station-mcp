using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Multiuser;
using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The project itself: opening, retrieving, creating, saving and closing it.
    ///
    /// Nothing else in this class works until one of these has succeeded, which is why every
    /// other file starts by asking whether a project is open rather than assuming one is.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Describes the projects open in TIA Portal.</summary>
        /// <returns>One description per open project. Empty when there is no portal.</returns>
        public IReadOnlyList<ObjectDescription> GetProjects()
        {
            return Describe(FindProjects());
        }

        /// <summary>Describes the multiuser sessions open in TIA Portal.</summary>
        /// <returns>One description per open session. Empty when there is none.</returns>
        public IReadOnlyList<ObjectDescription> GetSessions()
        {
            return Describe(FindSessions());
        }

        private static List<ObjectDescription> Describe(IEnumerable<ProjectBase> projects)
        {
            var described = new List<ObjectDescription>();

            foreach (var project in projects)
            {
                described.Add(ObjectDescriber.Describe(project, project.Name));
            }

            return described;
        }

        private List<ProjectBase> FindProjects()
        {
            _logger?.LogInformation("Getting open projects...");

            if (_portal == null)
            {
                _logger?.LogWarning("No TIA Portal instance available.");

                return [];
            }

            var projects = new List<ProjectBase>();

            if (_portal.Projects != null)
            {
                foreach (var project in _portal.Projects)
                {
                    projects.Add(project);
                }
            }

            return projects;
        }

        /// <summary>
        /// Opens a local project, or takes it where it is already open, without closing a project
        /// that belongs to somebody else. <see cref="ProjectOpeningRule"/> decides how.
        /// </summary>
        /// <param name="projectPath">The project file, <c>.apNN</c>.</param>
        /// <returns>True when the project is open and current; false when no portal is connected.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when the user's TIA Portal holds another
        /// project; an operation failure when TIA Portal could not open it.
        /// </exception>
        public bool OpenProject(string projectPath)
        {
            _logger?.LogInformation("Opening project: {ProjectPath}", projectPath);

            if (IsPortalNull())
            {
                return false;
            }

            try
            {
                var requested = FullProjectPath(projectPath);

                if (requested.Length == 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "A project path is required");
                }

                var openPath = ProjectPathInAttachedPortal(requested);

                return CarryOut(DecideOpening(requested, openPath), requested, openPath);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Failed to open project: {ex.Message}", null, ex);

                pex.Data["projectPath"] = projectPath;
                pex.Data["attachedProcess"] = AttachedProcess?.ToString();

                _logger?.LogError(pex, "OpenProject failed for {ProjectPath} in {Process}", projectPath, AttachedProcess);
                throw pex;
            }
        }

        /// <summary>
        /// Retrieves a project archive (<c>.zapNN</c>) into <paramref name="targetDirectory"/> and
        /// leaves the resulting project open. This is how TIA projects are moved between machines,
        /// so it is the entry point for putting an archived project under version control.
        /// </summary>
        /// <param name="archivePath">Full path of the archive to retrieve.</param>
        /// <param name="targetDirectory">
        /// Directory the project is extracted into. TIA Portal creates a subfolder named after the
        /// archive; if that subfolder already exists the call is refused rather than overwriting it.
        /// </param>
        /// <returns>The full path of the retrieved project file.</returns>
        /// <exception cref="PortalException">
        /// The arguments are invalid, the archive is missing, the target already exists, or
        /// TIA Portal failed to retrieve the archive.
        /// </exception>
        public string RetrieveProject(string archivePath, string targetDirectory)
        {
            _logger?.LogInformation("Retrieving archive {ArchivePath} into {TargetDirectory}...", archivePath, targetDirectory);

            try
            {
                ValidateRetrieveRequest(archivePath, targetDirectory);
                MakeRoomForNewProject(targetDirectory);

                // Retrieve() upgrades nothing: an archive from an older TIA version needs
                // RetrieveWithUpgrade instead, which rewrites the project irreversibly.
                // Staying on Retrieve keeps this operation non-destructive by default.
                CurrentProject = _portal!.Projects.Retrieve(new FileInfo(archivePath), new DirectoryInfo(targetDirectory));

                if (CurrentProject == null)
                {
                    throw new PortalException(PortalErrorCode.RetrieveFailed, $"TIA Portal returned no project for archive: {archivePath}");
                }

                return CurrentProject.Path.FullName;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.RetrieveFailed, $"Retrieve failed: {ex.Message}", null, ex);

                pex.Data["archivePath"] = archivePath;
                pex.Data["targetDirectory"] = targetDirectory;

                _logger?.LogError(pex, "RetrieveProject failed for {ArchivePath} -> {TargetDirectory}", archivePath, targetDirectory);
                throw pex;
            }
        }

        /// <summary>Creates an empty project and opens it.</summary>
        /// <param name="targetDirectory">Directory the project is created in. Must not already hold one.</param>
        /// <param name="projectName">Name of the project.</param>
        /// <returns>Full path of the created project.</returns>
        /// <remarks>
        /// Building a fixture from scratch is the alternative to inheriting one: a project created
        /// here has no protection level, no user management and no settings nobody remembers
        /// making. That matters for the test bench, where an unexplained inherited setting is
        /// indistinguishable from a defect in this server.
        /// </remarks>
        /// <exception cref="PortalException">Not connected, or the directory already holds a project.</exception>
        public string CreateProject(string targetDirectory, string projectName)
        {
            _logger?.LogInformation("Creating project {ProjectName} in {TargetDirectory}...", projectName, targetDirectory);

            try
            {
                ValidateCreateRequest(targetDirectory, projectName);
                MakeRoomForNewProject(Path.Combine(targetDirectory, projectName));

                CurrentProject = _portal!.Projects.Create(new DirectoryInfo(targetDirectory), projectName);

                if (CurrentProject == null)
                {
                    throw new PortalException(PortalErrorCode.RetrieveFailed, $"TIA Portal returned no project for '{projectName}'");
                }

                return CurrentProject.Path.FullName;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.RetrieveFailed, $"Create failed: {ex.Message}", null, ex);

                pex.Data["targetDirectory"] = targetDirectory;
                pex.Data["projectName"] = projectName;

                _logger?.LogError(pex, "CreateProject failed for {ProjectName} in {TargetDirectory}", projectName, targetDirectory);
                throw pex;
            }
        }

        private void ValidateCreateRequest(string targetDirectory, string projectName)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "targetDirectory is required");
            }

            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "projectName is required");
            }

            if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || projectName.Trim('.').Length == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"projectName '{projectName}' must be a plain folder name: no path separators, no '..'");
            }

            if (IsPortalNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Connect to TIA Portal first");
            }

            // TIA Portal creates the project in a subfolder named after it. Checked here so an
            // existing folder is refused with its path, not with whatever Openness says about it.
            var projectDirectory = Path.Combine(targetDirectory, projectName);
            if (Directory.Exists(projectDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Target already exists, refusing to overwrite: {projectDirectory}");
            }
        }

        private void ValidateRetrieveRequest(string archivePath, string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "archivePath is required");
            }

            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "targetDirectory is required");
            }

            if (IsPortalNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Connect to TIA Portal before retrieving a project");
            }

            if (!File.Exists(archivePath))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Archive not found: {archivePath}");
            }

            var projectDirectory = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(archivePath));
            if (Directory.Exists(projectDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Target already exists, refusing to overwrite: {projectDirectory}");
            }
        }

        public object? GetProjectInfo()
        {
            _logger?.LogInformation("Getting project info...");

            if (IsPortalNull())
            {
                return null;
            }

            if (IsProjectNull())
            {
                return null;
            }

            var project = CurrentProject!;

            var info = new
            {
                Name = project.Name,
                Path = project.Path,
                Type = project.GetType().Name,
                IsMultiuserProject = project is MultiuserProject,
                IsLocalSession = _session != null,
                IsLocalProject = _session == null
            };

            return info;
        }

        public bool SaveProject()
        {
            _logger?.LogInformation("Saving project...");

            if (IsProjectNull())
            {
                return false;
            }

            (CurrentProject as Project)?.Save();

            return true;
        }

        public bool SaveAsProject(string path)
        {
            _logger?.LogInformation($"Saving project as: {path}");

            if (IsProjectNull())
            {
                return false;
            }

            var di = new DirectoryInfo(path);

            (CurrentProject as Project)?.SaveAs(di);

            // The remembered path read anew: if the open project now lives at the new path, GetState
            // would otherwise find the old one closed and let go of it.
            OpenProjectPath = CurrentProject?.Path?.FullName ?? string.Empty;

            return true;
        }

        public bool CloseProject()
        {
            _logger?.LogInformation("Closing project...");

            if (IsProjectNull())
            {
                return false;
            }

            (CurrentProject as Project)?.Close();
            CurrentProject = null;

            return true;
        }
    }
}
