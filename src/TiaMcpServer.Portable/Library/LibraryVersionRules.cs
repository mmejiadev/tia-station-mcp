using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>Which version of a project library type may be discarded, made the default, or deleted.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21, on a copy of a class project: <c>Delete</c> on the default version is refused
    /// ("cannot be deleted because it is a default version"), as is a version other types depend on, and a type with
    /// instances; a version that is neither deletes. <c>SetAsDefault</c> takes any released version and back. Checked here
    /// first so the answer names what to do instead, and so nothing is archived for a call that cannot run; what only
    /// TIA Portal knows - dependents, instances - it still refuses itself.
    /// </remarks>
    public static class LibraryVersionRules
    {
        /// <summary>Chooses the version in work a discard throws away.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="versions">Every version the type holds.</param>
        /// <returns>The number of the version in work.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when no version is in work.</exception>
        /// <remarks>
        /// When the version in work is the type's only one, discarding it removes the type (measured 2026-11-15 on the
        /// class project 0967_1); <see cref="RemovesTheType"/> tells the caller so.
        /// </remarks>
        public static Version RequireDiscardable(string typeName, IReadOnlyList<LibraryVersionInfo> versions)
        {
            var inWork = RequireVersions(versions).Where(version => !version.IsReleased).ToList();

            return inWork.Count == 1
                ? inWork[0].Number
                : throw new PortalException(PortalErrorCode.InvalidState, inWork.Count == 0
                    ? $"'{typeName}' has no version in work to discard; its versions are {Describe(versions)}"
                    : $"'{typeName}' has {inWork.Count} versions in work, which TIA Portal does not allow; look at it in TIA Portal");
        }

        /// <summary>Whether discarding the version in work takes the whole type with it.</summary>
        /// <param name="versions">Every version the type holds.</param>
        /// <returns>True when the version in work is its only one.</returns>
        public static bool RemovesTheType(IReadOnlyList<LibraryVersionInfo> versions)
        {
            return RequireVersions(versions).Count == 1 && !versions[0].IsReleased;
        }

        /// <summary>Checks a whole type can be deleted, as far as its versions tell.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="versions">Every version the type holds.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when a version is in work.</exception>
        /// <remarks>
        /// Measured 2026-11-15: TIA Portal refuses to delete a type with a version "in test/in work". Instances and
        /// dependents it refuses itself.
        /// </remarks>
        public static void RequireTypeDeletable(string typeName, IReadOnlyList<LibraryVersionInfo> versions)
        {
            var inWork = RequireVersions(versions).FirstOrDefault(version => !version.IsReleased);

            if (inWork != null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"{LibraryVersionNumber.Format(inWork.Number)} of '{typeName}' is in work, and TIA Portal does not delete a type with a version in work: DiscardLibraryTypeEdit throws it away (and the type with it, when it is the only version)");
            }
        }

        /// <summary>Checks a version can become the type's default.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="versions">Every version the type holds.</param>
        /// <param name="requested">The version asked for.</param>
        /// <returns>The version, in three parts.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the type has no such version, or it is in work.</exception>
        public static Version RequireDefaultable(string typeName, IReadOnlyList<LibraryVersionInfo> versions, Version requested)
        {
            var version = RequireVersion(typeName, versions, requested);

            return version.IsReleased
                ? version.Number
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{LibraryVersionNumber.Format(version.Number)} of '{typeName}' is in work; only a released version can be the default (ReleaseLibraryType releases it)");
        }

        /// <summary>Checks a version can be deleted.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="versions">Every version the type holds.</param>
        /// <param name="requested">The version asked for.</param>
        /// <returns>The version, in three parts.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when the type has no such version;
        /// <see cref="PortalErrorCode.InvalidState"/> when it is in work, the default, or the type's only version.
        /// </exception>
        public static Version RequireDeletable(string typeName, IReadOnlyList<LibraryVersionInfo> versions, Version requested)
        {
            var version = RequireVersion(typeName, versions, requested);
            var number = LibraryVersionNumber.Format(version.Number);

            if (!version.IsReleased)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"{number} of '{typeName}' is in work: DiscardLibraryTypeEdit throws a version in work away");
            }

            // Before the default check: a type's only released version is always its default, and SetDefault has nothing to offer then.
            if (RequireVersions(versions).Count == 1)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"{number} is the only version of '{typeName}': DeleteLibraryType deletes the type");
            }

            // Review of 2026-11-16: beside a version in work, "make another the default" had no version to name.
            if (versions.Count(other => other.IsReleased) == 1)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"{number} is the only released version of '{typeName}', so its default, and the other is in work: release that one (ReleaseLibraryType) and make it the default first, or discard it (DiscardLibraryTypeEdit) and delete the type (DeleteLibraryType)");
            }

            return !version.IsDefault
                ? version.Number
                : throw new PortalException(PortalErrorCode.InvalidState, $"{number} is the default version of '{typeName}', which TIA Portal does not delete: make another one the default first (SetDefaultLibraryTypeVersion)");
        }

        private static LibraryVersionInfo RequireVersion(string typeName, IReadOnlyList<LibraryVersionInfo> versions, Version requested)
        {
            if (requested == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "version is required");
            }

            var number = LibraryVersionNumber.Normalise(requested);

            return RequireVersions(versions).FirstOrDefault(version => version.Number == number)
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{typeName}' has no version {LibraryVersionNumber.Format(number)}; it has {Describe(versions)}");
        }

        private static IReadOnlyList<LibraryVersionInfo> RequireVersions(IReadOnlyList<LibraryVersionInfo> versions)
        {
            return versions ?? throw new ArgumentNullException(nameof(versions));
        }

        private static string Describe(IReadOnlyList<LibraryVersionInfo> versions)
        {
            return string.Join(", ", versions.Select(version =>
                LibraryVersionNumber.Format(version.Number) + (version.IsReleased ? string.Empty : " (in work)") + (version.IsDefault ? " (default)" : string.Empty)));
        }
    }
}
