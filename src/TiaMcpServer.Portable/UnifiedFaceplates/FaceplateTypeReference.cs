using System;
using TiaMcpServer.Library;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>How a WinCC Unified faceplate container names the library type and version it shows.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: <c>ScreenItems.Create&lt;HmiFaceplateContainer&gt;(name, "V0.0.1\FACEPLATE_MOTOR_V1")</c>
    /// places one, and <c>ContainedType</c> reads back the same string. The type name alone, "V 0.0.1"
    /// with a space ("Unable to get version number details from input") and a version in work ("Faceplate
    /// type with the specified faceplate name is unavailable") are refused.
    /// </remarks>
    public static class FaceplateTypeReference
    {
        private const char Separator = '\\';

        /// <summary>Writes the reference to a released version.</summary>
        /// <param name="typeName">The library type's name.</param>
        /// <param name="version">The released version.</param>
        /// <returns>E.g. <c>V0.0.1\FACEPLATE_MOTOR_V1</c>.</returns>
        public static string Format(string typeName, Version version)
        {
            return $"V{LibraryVersionNumber.Format(version)}{Separator}{typeName}";
        }

        /// <summary>Reads a reference back, as a container's <c>ContainedType</c> gives it.</summary>
        /// <param name="reference">E.g. <c>V0.0.1\FACEPLATE_MOTOR_V1</c>.</param>
        /// <returns>The type name and version, or null when the text is not a reference of that shape.</returns>
        public static (string TypeName, Version Version)? Parse(string reference)
        {
            var text = reference ?? string.Empty;
            var separator = text.IndexOf(Separator);

            // Version.TryParse forgives the space TIA Portal refuses, so the number must start right after the V.
            if (separator < 2 || text[0] != 'V' || !char.IsDigit(text[1]) || separator == text.Length - 1)
            {
                return null;
            }

            return Version.TryParse(text.Substring(1, separator - 1), out var version)
                ? (text.Substring(separator + 1), version)
                : ((string, Version)?)null;
        }
    }
}
