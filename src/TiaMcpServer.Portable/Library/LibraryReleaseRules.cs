using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>Which version of a library type a release turns into a released one, and under which number.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: <c>Release</c> takes any number, a repeated one included,
    /// and the type then holds two released versions numbered alike — <c>0.0.1</c> twice — which no
    /// instance can tell apart. So the number must be higher than every released one, checked here
    /// before TIA Portal is asked. A released version cannot be released again ("Error when calling
    /// method 'Release'"), so the one released is the type's single version in work.
    /// </remarks>
    public static class LibraryReleaseRules
    {
        /// <summary>Chooses the number to release a type's version in work under.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="versions">Every version the type holds.</param>
        /// <param name="requested">The number asked for, or null for the number the version in work already has.</param>
        /// <returns>The number to release under.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when no version, or more than one, is in work;
        /// <see cref="PortalErrorCode.InvalidParams"/> when the number is not higher than every released one.
        /// </exception>
        public static Version Choose(string typeName, IReadOnlyList<LibraryVersionInfo> versions, Version? requested)
        {
            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            var inWork = RequireOneInWork(typeName, versions);
            var number = requested == null ? inWork.Number : LibraryVersionNumber.Normalise(requested);
            var highest = versions.Where(version => version.IsReleased).Select(version => version.Number).OrderBy(found => found).LastOrDefault();

            if (highest != null && number <= highest)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{typeName}' cannot be released as {LibraryVersionNumber.Format(number)}: {LibraryVersionNumber.Format(highest)} is already released, and the number must be higher. "
                    + "TIA Portal would release a repeated number and keep two versions under it.");
            }

            return number;
        }

        /// <summary>Throws when the version to release depends on a type still in work.</summary>
        /// <param name="typeName">The type being released.</param>
        /// <param name="dependencies">Each type the version in work depends on, and whether that version is released.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the types still in work.</exception>
        /// <remarks>
        /// The release is asked never to create or release another type. Measured 2026-10-30: the other mode,
        /// <c>AutomaticallyCreateOrReleaseDependenciesIfRequired</c>, gave the FB using a released UDT a new
        /// version in work without a word. So a type that still needs another released is refused, naming it,
        /// rather than having that other type changed behind the caller's back.
        /// </remarks>
        public static void RequireDependenciesReleased(string typeName, IEnumerable<(string Name, bool IsReleased)> dependencies)
        {
            var unreleased = (dependencies ?? Array.Empty<(string, bool)>()).Where(dependency => !dependency.IsReleased).Select(dependency => dependency.Name).ToList();

            if (unreleased.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{typeName}' depends on '{string.Join("', '", unreleased)}', still in work; release that first, so this release changes nothing else");
            }
        }

        private static LibraryVersionInfo RequireOneInWork(string typeName, IReadOnlyList<LibraryVersionInfo> versions)
        {
            var inWork = versions.Where(version => !version.IsReleased).ToList();

            if (inWork.Count == 1)
            {
                return inWork[0];
            }

            var held = string.Join(", ", versions.Select(version => $"{LibraryVersionNumber.Format(version.Number)} {(version.IsReleased ? "released" : "in work")}"));

            throw new PortalException(
                PortalErrorCode.InvalidState,
                inWork.Count == 0
                    ? $"'{typeName}' has no version in work, so there is nothing to release ({held}). Edit the type in TIA Portal first; that makes a version in work."
                    : $"'{typeName}' has {inWork.Count} versions in work ({held}); release or discard them in TIA Portal, as which one is meant cannot be told.");
        }
    }
}
