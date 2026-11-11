using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Bringing a project from an older TIA Portal to this one. Its own file because an upgrade is
    /// the one way of opening a project that writes a new one to disk, so it is asked for by name
    /// and never happens as a side effect of opening. <see cref="ProjectUpgradeRule"/> decides,
    /// and is tested without TIA Portal.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Opens a project of an older TIA Portal as an upgraded copy, and leaves the copy open.</summary>
        /// <param name="projectPath">The project file, <c>.apNN</c>, of an earlier version.</param>
        /// <returns>The full path of the upgraded project file.</returns>
        /// <exception cref="PortalException">
        /// The path is invalid or missing, the file is not older than this TIA Portal, the copy's
        /// folder already exists, or TIA Portal failed to upgrade it.
        /// </exception>
        public string OpenProjectWithUpgrade(string projectPath)
        {
            _logger?.LogInformation("Upgrading project {ProjectPath}...", projectPath);

            try
            {
                var requested = FullProjectPath(projectPath);

                ValidateProjectUpgrade(requested);
                MakeRoomForNewProject(requested);

                _session = null;
                CurrentProject = _portal!.Projects.OpenWithUpgrade(new FileInfo(requested));

                return RequireUpgradedProject(requested);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.RetrieveFailed, $"Upgrade failed: {ex.Message}", null, ex);

                pex.Data["projectPath"] = projectPath;

                _logger?.LogError(pex, "OpenProjectWithUpgrade failed for {ProjectPath}", projectPath);
                throw pex;
            }
        }

        /// <summary>Retrieves an archive of an older TIA Portal as an upgraded project, and leaves it open.</summary>
        /// <param name="archivePath">The archive, <c>.zapNN</c>, of an earlier version.</param>
        /// <param name="targetDirectory">Directory the project is extracted into.</param>
        /// <returns>The full path of the upgraded project file.</returns>
        /// <exception cref="PortalException">
        /// The arguments are invalid, the archive is missing or not older than this TIA Portal, the
        /// target already exists, or TIA Portal failed to upgrade it.
        /// </exception>
        public string RetrieveProjectWithUpgrade(string archivePath, string targetDirectory)
        {
            _logger?.LogInformation("Upgrading archive {ArchivePath} into {TargetDirectory}...", archivePath, targetDirectory);

            try
            {
                ProjectUpgradeRule.RequireUpgradableArchive(archivePath, Engineering.TiaMajorVersion);
                ValidateRetrieveTarget(archivePath, targetDirectory);
                MakeRoomForNewProject(targetDirectory);

                CurrentProject = _portal!.Projects.RetrieveWithUpgrade(new FileInfo(archivePath), new DirectoryInfo(targetDirectory));

                return RequireUpgradedProject(archivePath);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.RetrieveFailed, $"Upgrade failed: {ex.Message}", null, ex);

                pex.Data["archivePath"] = archivePath;
                pex.Data["targetDirectory"] = targetDirectory;

                _logger?.LogError(pex, "RetrieveProjectWithUpgrade failed for {ArchivePath} -> {TargetDirectory}", archivePath, targetDirectory);
                throw pex;
            }
        }

        /// <remarks>
        /// Measured 2026-11-11 on V21: the folders are named after the project inside the archive, not
        /// the archive (<c>UnifiedProject.zap20</c> gave <c>0967-unified_00</c>, its <c>_V21</c> and its
        /// <c>.backup</c>), so what an archive would collide with cannot be told before retrieving it.
        /// </remarks>
        private static void RequireEmptyTarget(string targetDirectory)
        {
            if (Directory.Exists(targetDirectory) && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Target directory is not empty, refusing to write into it: {targetDirectory}");
            }
        }

        private void ValidateProjectUpgrade(string projectFile)
        {
            if (projectFile.Length == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A project path is required");
            }

            if (IsPortalNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Connect to TIA Portal before upgrading a project");
            }

            if (!File.Exists(projectFile))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Project not found: {projectFile}");
            }

            ProjectUpgradeRule.RequireUpgradableProject(projectFile, Engineering.TiaMajorVersion);

            var upgradedFolder = ProjectUpgradeRule.UpgradedFolder(projectFile, Engineering.TiaMajorVersion);
            if (Directory.Exists(upgradedFolder))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Target already exists, refusing to overwrite: {upgradedFolder}");
            }
        }

        /// <remarks>
        /// What came back is checked rather than trusted: a project still of the old version would
        /// be reported as upgraded, and the next write would land in a project nobody asked for.
        /// </remarks>
        private string RequireUpgradedProject(string source)
        {
            var upgraded = CurrentProject?.Path?.FullName;

            if (upgraded == null)
            {
                throw new PortalException(PortalErrorCode.RetrieveFailed, $"TIA Portal returned no project for: {source}");
            }

            if (ProjectUpgradeRule.MajorVersionOf(upgraded) != Engineering.TiaMajorVersion)
            {
                (CurrentProject as Project)?.Close();
                CurrentProject = null;

                throw new PortalException(PortalErrorCode.RetrieveFailed, $"TIA Portal opened '{upgraded}', which is not a V{Engineering.TiaMajorVersion} project, and it was closed. What it wrote stays on disk: remove it before asking again, as a copy already there is refused");
            }

            return upgraded;
        }
    }
}
