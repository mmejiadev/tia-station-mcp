using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>Decides, from its file name, whether a project, an archive or a global library has to be upgraded.</summary>
    /// <remarks>
    /// The extension is the one version a file gives away before TIA Portal has opened it, and an
    /// upgrade is not something to find out about afterwards: it writes a new project folder.
    ///
    /// Measured 2026-11-10 on TIA Portal V21: <c>OpenWithUpgrade</c> leaves the original as it was and
    /// creates a sibling folder named after the project's folder, not its file, so a project in
    /// <c>p</c> lands in <c>p_V21</c>, beside a <c>p_V21.backup</c>. Measured again 2026-11-11 through
    /// the tools, on three archives and a project copy: each original unchanged, byte for byte.
    /// </remarks>
    public static class ProjectUpgradeRule
    {
        private const string ProjectKind = "ap";
        private const string ArchiveKind = "zap";
        private const string LibraryKind = "al";

        private static readonly Regex VersionedExtension =
            new Regex(@"\.(zap|ap|al)(\d{2,3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The TIA Portal version a project file, an archive or a global library was made with.</summary>
        /// <param name="path">A path ending in <c>.apNN</c>, <c>.zapNN</c> or <c>.alNN</c>.</param>
        /// <returns>The major version, or null when the path ends in none of them.</returns>
        public static int? MajorVersionOf(string path)
        {
            var match = Read(path);

            return match.Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : null;
        }

        /// <summary>Refuses a file an older TIA Portal made, which only an upgrade can read.</summary>
        /// <param name="path">The project file, the archive or the global library file.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <param name="upgradeTool">The tool that upgrades it, named in the refusal.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when the file is older.</exception>
        /// <remarks>A path with no version, such as a multiuser session, is not this rule's to refuse.</remarks>
        public static void RequireNoUpgradeNeeded(string path, int portalMajorVersion, string upgradeTool)
        {
            var version = MajorVersionOf(path);

            if (version.HasValue && version.Value < portalMajorVersion)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{path}' was made with TIA Portal V{version.Value} and this is V{portalMajorVersion}. Nothing was opened: " +
                    $"{upgradeTool} makes an upgraded copy and leaves this file as it is.");
            }
        }

        /// <summary>Refuses anything but a project file an older TIA Portal made.</summary>
        /// <param name="path">The project file.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when the path is no project file;
        /// <see cref="PortalErrorCode.InvalidState"/> when it is of this version or a later one.
        /// </exception>
        public static void RequireUpgradableProject(string path, int portalMajorVersion)
        {
            RequireOlder(path, portalMajorVersion, ProjectKind, "a project file (.apNN)");
        }

        /// <summary>Refuses anything but a project archive an older TIA Portal made.</summary>
        /// <param name="path">The archive.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when the path is no archive;
        /// <see cref="PortalErrorCode.InvalidState"/> when it is of this version or a later one.
        /// </exception>
        public static void RequireUpgradableArchive(string path, int portalMajorVersion)
        {
            RequireOlder(path, portalMajorVersion, ArchiveKind, "a project archive (.zapNN)");
        }

        /// <summary>Refuses anything but a global library file an older TIA Portal made.</summary>
        /// <param name="path">The library file.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when the path is no library file;
        /// <see cref="PortalErrorCode.InvalidState"/> when it is of this version or a later one.
        /// </exception>
        public static void RequireUpgradableLibrary(string path, int portalMajorVersion)
        {
            RequireOlder(path, portalMajorVersion, LibraryKind, "a global library file (.alNN)");
        }

        /// <summary>The folder TIA Portal creates for the upgraded copy of a project or a global library.</summary>
        /// <param name="file">Full path of the project or library file being upgraded.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <returns>The sibling of the file's folder, for example <c>C:\Class\Motor_V21</c>.</returns>
        /// <remarks>
        /// Measured 2026-11-11 on V21 for a library: when that folder exists, TIA Portal writes to
        /// <c>Motor_V21_1</c> without a word. Callers refuse an existing one rather than let it.
        /// </remarks>
        public static string UpgradedFolder(string file, int portalMajorVersion)
        {
            return $"{Path.GetDirectoryName(file)}_V{portalMajorVersion.ToString(CultureInfo.InvariantCulture)}";
        }

        private static Match Read(string path)
        {
            return VersionedExtension.Match((path ?? string.Empty).Trim());
        }

        private static void RequireOlder(string path, int portalMajorVersion, string kind, string expected)
        {
            var match = Read(path);

            if (!match.Success || !string.Equals(match.Groups[1].Value, kind, StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' is not {expected}");
            }

            var version = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

            if (version >= portalMajorVersion)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{path}' is for TIA Portal V{version} and this is V{portalMajorVersion}, so there is nothing to upgrade it to. " +
                    "Open it as it is.");
            }
        }
    }
}
