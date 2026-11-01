using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>A library type version number as TIA Portal writes it: three parts, <c>0.0.1</c>.</summary>
    public static class LibraryVersionNumber
    {
        private const int Parts = 3;

        /// <summary>Reads a version number.</summary>
        /// <param name="text">The number, e.g. <c>0.0.2</c>.</param>
        /// <param name="what">What the number is for, named in a refusal.</param>
        /// <returns>The version, with exactly three parts.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is not three whole numbers.</exception>
        public static Version Parse(string text, string what)
        {
            var trimmed = (text ?? string.Empty).Trim();

            if (!Version.TryParse(trimmed, out var version) || version.Revision != -1 || version.Build == -1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{text}' is not a version number of three parts, e.g. \"0.0.2\"");
            }

            return version;
        }

        /// <summary>A version as three parts, so one read from TIA Portal and one parsed here compare equal.</summary>
        /// <param name="version">The version, with any number of parts.</param>
        /// <returns><c>0.0.1</c> for <c>0.0.1.0</c>, <c>0.1</c> or <c>0.0.1</c>.</returns>
        /// <remarks>
        /// <see cref="Version"/> equality counts the parts: <c>0.0.1</c> and <c>0.0.1.0</c> differ. What Openness
        /// gives as <c>VersionNumber</c> is printed in three parts, but its unfilled parts were not measured.
        /// </remarks>
        public static Version Normalise(Version version)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }

        /// <summary>Writes a version number as TIA Portal does.</summary>
        /// <param name="version">The version.</param>
        /// <returns><c>0.0.1</c>.</returns>
        public static string Format(Version version)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            return version.ToString(Parts);
        }
    }
}
