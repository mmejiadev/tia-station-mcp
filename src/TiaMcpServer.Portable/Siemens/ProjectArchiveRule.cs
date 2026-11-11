using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>Decides the file a project archive is written to.</summary>
    /// <remarks>
    /// TIA Portal V21 writes exactly the name it is given and adds no extension (measured 2026-11-11:
    /// <c>Motor</c> became a file called <c>Motor</c>), so the extension of the version in use is added
    /// here. The name is asked for without one: a caller writing <c>Motor.zap20</c> on V21 would
    /// otherwise get a file whose name says one version and whose content is another.
    /// </remarks>
    public static class ProjectArchiveRule
    {
        private static readonly Regex ArchiveExtension =
            new Regex(@"\.zap\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The archive's file name for a name and a TIA Portal version.</summary>
        /// <param name="archiveName">The name, without extension, e.g. <c>Motor</c>.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <returns>The file name, for example <c>Motor.zap21</c>.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when the name is empty, holds a path, or carries an archive extension.
        /// </exception>
        public static string ArchiveFileName(string archiveName, int portalMajorVersion)
        {
            var name = (archiveName ?? string.Empty).Trim();

            if (name.Trim('.').Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"archiveName '{archiveName}' must be a plain file name: no folder, no path separators");
            }

            if (ArchiveExtension.IsMatch(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Give archiveName without its extension: '.zap{portalMajorVersion}' is added to it");
            }

            return $"{name}.zap{portalMajorVersion.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
