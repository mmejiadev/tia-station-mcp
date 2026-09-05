using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Multiuser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public bool OpenProject(string projectPath)
        {
            _logger?.LogInformation($"Opening project: {projectPath}");

            if (IsPortalNull())
            {
                return false;
            }

            if (_project != null)
            {
                (_project as Project)?.Close();
                _project = null;
            }

            if (_session != null)
            {
                _session.Close();
                _session = null;
            }

            try
            {
                var projects = FindProjects();
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                if (!string.IsNullOrEmpty(projectName) && projects.Any(p => p.Name.Equals(projectName)))
                {
                    // Project is already open
                    _project = _portal?.Projects.FirstOrDefault(p => p.Name == projectName);

                    return _project != null;
                }
                else
                {
                    // see [5.3.1 Projekt öffnen, S.113]
                    _project = _portal?.Projects.OpenWithUpgrade(new FileInfo(projectPath));

                    return _project != null;
                }
            }
            catch (Exception)
            {
                return false;
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
                CloseOpenProject();

                // Retrieve() upgrades nothing: an archive from an older TIA version needs
                // RetrieveWithUpgrade instead, which rewrites the project irreversibly.
                // Staying on Retrieve keeps this operation non-destructive by default.
                _project = _portal!.Projects.Retrieve(new FileInfo(archivePath), new DirectoryInfo(targetDirectory));

                if (_project == null)
                {
                    throw new PortalException(PortalErrorCode.RetrieveFailed, $"TIA Portal returned no project for archive: {archivePath}");
                }

                return _project.Path.FullName;
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
                CloseOpenProject();

                _project = _portal!.Projects.Create(new DirectoryInfo(targetDirectory), projectName);

                if (_project == null)
                {
                    throw new PortalException(PortalErrorCode.RetrieveFailed, $"TIA Portal returned no project for '{projectName}'");
                }

                return _project.Path.FullName;
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

            if (IsPortalNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Connect to TIA Portal first");
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

        private void CloseOpenProject()
        {
            (_project as Project)?.Close();
            _project = null;

            _session?.Close();
            _session = null;
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

            var project = _project!;

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

            (_project as Project)?.Save();

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

            (_project as Project)?.SaveAs(di);

            return true;
        }

        public bool CloseProject()
        {
            _logger?.LogInformation("Closing project...");

            if (IsProjectNull())
            {
                return false;
            }

            (_project as Project)?.Close();
            _project = null;

            return true;
        }
    }
}
