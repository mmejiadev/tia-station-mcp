using System;
using Siemens.Engineering.SW;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the caller's import option into the value Openness expects.
    /// </summary>
    /// <remarks>
    /// The option arrives as a string because that is what an MCP tool argument is, and it has to
    /// become <see cref="ImportDocumentOptions"/>, which is an Openness type. Doing that conversion
    /// here is what keeps the enum out of <c>ModelContextProtocol/</c>, where it had no business
    /// being: the layer above now passes the word it was given and never names the type.
    ///
    /// The aliases are inherited and deliberately kept. A caller writing "skipInactive" means one
    /// thing only, and refusing it teaches nobody anything.
    /// </remarks>
    public static class ImportDocumentOption
    {
        /// <summary>Reads an option.</summary>
        /// <param name="option">The caller's word, or empty for the default.</param>
        /// <returns>The Openness option. An empty string means <c>Override</c>.</returns>
        /// <exception cref="PortalException">The word names no option.</exception>
        public static ImportDocumentOptions Parse(string option)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                return ImportDocumentOptions.Override;
            }

            var normalized = option.Trim();

            if (Enum.TryParse<ImportDocumentOptions>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return FromAlias(normalized, option);
        }

        /// <summary>Refuses an option the import would not understand, before anything is planned.</summary>
        /// <param name="option">The caller's word, or empty for the default.</param>
        /// <exception cref="PortalException">The word names no option.</exception>
        /// <remarks>
        /// Called by the write tools so that a mistyped option is refused as invalid input rather
        /// than becoming a change plan that fails halfway through the import.
        /// </remarks>
        public static void Validate(string option)
        {
            Parse(option);
        }

        private static ImportDocumentOptions FromAlias(string normalized, string asGiven)
        {
            switch (normalized.ToLowerInvariant())
            {
                case "override": return ImportDocumentOptions.Override;
                case "none": return ImportDocumentOptions.None;
                case "skipinactiveculture":
                case "skipinactivecultures":
                case "skipinactive":
                case "skipinactivecult":
                    return ImportDocumentOptions.SkipInactiveCultures;
                case "activeinactiveculture":
                case "activateinactivecultures":
                case "activeinactivecultures":
                case "activateinactive":
                    return ImportDocumentOptions.ActivateInactiveCultures;
                default:
                    throw new PortalException(
                        PortalErrorCode.InvalidParams,
                        $"Invalid importOption '{asGiven}'. Allowed: None, Override, SkipInactiveCultures, ActivateInactiveCultures");
            }
        }
    }
}
