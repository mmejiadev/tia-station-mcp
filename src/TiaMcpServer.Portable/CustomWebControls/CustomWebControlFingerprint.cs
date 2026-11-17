using System;
using System.Linq;
using System.Security.Cryptography;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>What an installation plan says about the control it installs.</summary>
    /// <remarks>
    /// Security review of 2026-11-15: the person confirming a plan in Workshop Mode saw only the control's name, and a
    /// written definition can carry any markup and script. The plan now says whether the control is the catalogue's and,
    /// for a written one, gives a fingerprint of the page it becomes, so the audit can tell one content from another.
    /// </remarks>
    public static class CustomWebControlFingerprint
    {
        private const int ShownHexCharacters = 16;

        /// <summary>Describes a control for a plan.</summary>
        /// <param name="definition">The control.</param>
        /// <param name="fromCatalogue">Whether it was taken from this server's catalogue.</param>
        /// <returns>E.g. <c>catalogue TiaStation.Motor</c>, or <c>written definition Plant.Pump, page sha256 1f3a…</c>.</returns>
        public static string Describe(CustomWebControlDefinition definition, bool fromCatalogue)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return fromCatalogue
                ? $"catalogue {definition.Identity.Name}"
                : $"written definition {definition.Identity.Name}, page sha256 {Hash(CustomWebControlPageWriter.Write(definition))}";
        }

        private static string Hash(byte[] content)
        {
            using var sha256 = SHA256.Create();

            return string.Concat(sha256.ComputeHash(content).Take(ShownHexCharacters / 2).Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
