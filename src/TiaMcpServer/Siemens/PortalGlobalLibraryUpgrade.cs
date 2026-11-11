using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Bringing a global library from an older TIA Portal to this one: every library tool opens a
    /// library with <c>Open</c>, which refuses an older one, so the upgrade is asked for by name.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Opens a global library of an older TIA Portal with an upgrade, and closes the upgraded copy.</summary>
        /// <param name="libraryPath">The library file, <c>.alNN</c>, of an earlier version.</param>
        /// <returns>The full path of the upgraded library file, and a warning when it could not be closed.</returns>
        /// <exception cref="PortalException">
        /// The path is invalid or missing, the library is not older than this TIA Portal, it is open
        /// already, or TIA Portal failed to upgrade it.
        /// </exception>
        public (string LibraryPath, string? Warning) UpgradeGlobalLibrary(string libraryPath)
        {
            _logger?.LogInformation("Upgrading global library {LibraryPath}...", libraryPath);

            try
            {
                var file = ValidateLibraryUpgrade(libraryPath);
                var upgraded = RequirePortal().GlobalLibraries.OpenWithUpgrade(file);
                // The file, not its folder: measured 2026-11-11 on V21, Path gave ProbeFaceplates_V21.al21.
                var upgradedPath = upgraded.Path?.FullName ?? string.Empty;
                var warning = GlobalLibraryAccess.TryClose(upgraded);

                if (ProjectUpgradeRule.MajorVersionOf(upgradedPath) != Engineering.TiaMajorVersion)
                {
                    throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal opened '{upgradedPath}', which is not a V{Engineering.TiaMajorVersion} library. What it wrote beside the original stays there: remove it before asking again, as a copy already there is refused");
                }

                return (upgradedPath, warning);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Upgrade failed: {ex.Message}", null, ex);

                pex.Data["libraryPath"] = libraryPath;

                _logger?.LogError(pex, "UpgradeGlobalLibrary failed for {LibraryPath}", libraryPath);
                throw pex;
            }
        }

        private FileInfo ValidateLibraryUpgrade(string libraryPath)
        {
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "libraryPath is required: the .alNN file inside the library's folder");
            }

            var file = new FileInfo(libraryPath.Trim());

            ProjectUpgradeRule.RequireUpgradableLibrary(file.FullName, Engineering.TiaMajorVersion);

            if (!file.Exists)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Global library not found: {file.FullName}");
            }

            if (RequirePortal().GlobalLibraries.Any(library => GlobalLibraryAccess.IsTheFile(library, file)))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The global library '{file.Name}' is open in TIA Portal; close it first");
            }

            var upgradedFolder = ProjectUpgradeRule.UpgradedFolder(file.FullName, Engineering.TiaMajorVersion);
            if (Directory.Exists(upgradedFolder))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{upgradedFolder}' already exists, so TIA Portal would write a second copy beside it; nothing was upgraded");
            }

            return file;
        }
    }
}
