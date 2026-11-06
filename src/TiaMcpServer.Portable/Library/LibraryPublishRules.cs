using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>Which project library types may be published to a global library.</summary>
    /// <remarks>
    /// Measured 2026-11-01 on TIA Portal V20: publishing a type whose only version is in work does not fail, and
    /// copies nothing, without a word. So every type named must have a released version, checked here.
    /// </remarks>
    public static class LibraryPublishRules
    {
        /// <summary>Throws unless every type named exists in the project library with a released version.</summary>
        /// <param name="requested">The type names to publish.</param>
        /// <param name="held">Every type name the project library holds.</param>
        /// <param name="versionsOf">A named type's versions; asked only for the types named, so an unrelated type can neither cost a read nor block the publish.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when none is named, one is named twice or the project library lacks one;
        /// <see cref="PortalErrorCode.InvalidState"/> when one has no released version.
        /// </exception>
        public static void Check(IReadOnlyList<string> requested, IReadOnlyCollection<string> held, Func<string, IReadOnlyList<LibraryVersionInfo>> versionsOf)
        {
            if (versionsOf == null)
            {
                throw new ArgumentNullException(nameof(versionsOf));
            }

            LibraryCopyRules.CheckRequested(requested, held, LibraryCopyRules.ProjectLibrary);

            var unreleased = requested.Where(name => !versionsOf(name).Any(version => version.IsReleased)).ToList();
            if (unreleased.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{string.Join("', '", unreleased)}' has no released version, and TIA Portal would publish nothing without a word; ReleaseLibraryType releases it first");
            }
        }

        /// <summary>Throws when a type named did not reach the global library, so nothing is saved and called published.</summary>
        /// <param name="requested">The type names published.</param>
        /// <param name="held">Every type name the global library holds afterwards.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.WriteFailed"/> naming the types missing.</exception>
        /// <remarks>TIA Portal was measured to publish a type only in work as nothing at all, without a word; this catches any other such case.</remarks>
        public static void CheckPublished(IReadOnlyList<string> requested, IReadOnlyCollection<string> held)
        {
            var missing = requested.Where(name => !held.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal did not publish '{string.Join("', '", missing)}' and gave no error; the global library was left as it was");
            }
        }

        /// <summary>The released version an instance moves to: the one named, or the newest.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="versions">Every version the type holds.</param>
        /// <param name="requested">The version asked for, or null for the newest released.</param>
        /// <returns>The version, in three parts.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when the type has no released version;
        /// <see cref="PortalErrorCode.InvalidParams"/> when the one named is not released.
        /// </exception>
        public static Version ReleasedVersion(string typeName, IReadOnlyList<LibraryVersionInfo> versions, Version? requested)
        {
            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            var released = versions.Where(version => version.IsReleased).Select(version => version.Number).OrderBy(number => number).ToList();
            if (released.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{typeName}' has no released version; ReleaseLibraryType releases it first");
            }

            if (requested == null)
            {
                return released[released.Count - 1];
            }

            var number = LibraryVersionNumber.Normalise(requested);

            return released.Contains(number)
                ? number
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{typeName}' has no released version {LibraryVersionNumber.Format(number)}; released: {string.Join(", ", released.Select(LibraryVersionNumber.Format))}");
        }
    }
}
