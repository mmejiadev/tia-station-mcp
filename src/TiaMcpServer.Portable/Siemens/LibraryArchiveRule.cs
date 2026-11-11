using System.Globalization;

namespace TiaMcpServer.Siemens
{
    /// <summary>Decides the file a global library is archived to before it changes.</summary>
    /// <remarks>
    /// Measured 2026-11-11 on TIA Portal V21: a library archive is written under exactly the name it is given, with no
    /// extension, as a project archive is, so the library <c>Kit</c> was backed up as a file called <c>Kit</c> that nobody
    /// would know to retrieve. The extension of the version in use is added here.
    /// </remarks>
    public static class LibraryArchiveRule
    {
        /// <summary>The archive's file name for a library and a TIA Portal version.</summary>
        /// <param name="libraryName">The library's name, without extension, e.g. <c>Kit</c>.</param>
        /// <param name="portalMajorVersion">The major version of the TIA Portal in use.</param>
        /// <returns>The file name, for example <c>Kit.zal21</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when the version is not known.</exception>
        public static string ArchiveFileName(string libraryName, int portalMajorVersion)
        {
            if (portalMajorVersion < 1)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "The TIA Portal version is not known, so a library archive cannot be named for it");
            }

            return $"{(libraryName ?? string.Empty).Trim()}.zal{portalMajorVersion.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
