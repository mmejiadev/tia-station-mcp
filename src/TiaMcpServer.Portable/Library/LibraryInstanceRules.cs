using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>What a program's instances of a block or PLC data type library type allow: a new version through one, or moving them.</summary>
    /// <remarks>
    /// Measured 2026-11-24 on TIA Portal V21. <c>LibraryTypeVersion.Edit()</c> on a block or PLC data type type is refused, with an
    /// instance in the program or without one ("A test environment is required ... at least one instance of the type or version
    /// must be available in the project"); <c>Edit(LibraryTypeInstanceInfo)</c> with an instance of that version, read from
    /// <c>FindInstances</c>, starts the next version in work and moves that instance onto it. So there is no new version of such a
    /// type without an instance; the tools make one first when there is none.
    /// </remarks>
    public static class LibraryInstanceRules
    {
        /// <summary>Throws unless the program holds an instance of the version a new one is to start from.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="softwarePath">The program, named in a refusal.</param>
        /// <param name="newest">The newest released version, which the new one starts from.</param>
        /// <param name="instanceVersions">The version each of the program's instances of the type shows.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when no instance shows <paramref name="newest"/>, naming what to do.</exception>
        public static void RequireInstanceOn(string typeName, string softwarePath, Version newest, IReadOnlyList<Version> instanceVersions)
        {
            if (newest == null || instanceVersions == null)
            {
                throw new ArgumentNullException(newest == null ? nameof(newest) : nameof(instanceVersions));
            }

            var number = LibraryVersionNumber.Normalise(newest);
            var shown = instanceVersions.Select(LibraryVersionNumber.Normalise).Distinct().ToList();
            if (shown.Contains(number))
            {
                return;
            }

            throw new PortalException(PortalErrorCode.InvalidState, shown.Count == 0
                ? $"'{softwarePath}' holds no instance of '{typeName}', and TIA Portal starts a new version of a block or data type only through one; CreatePlcBlockFromLibrary or CreatePlcTypeFromLibrary creates it from {LibraryVersionNumber.Format(number)} first"
                : $"The instance of '{typeName}' in '{softwarePath}' shows {string.Join(", ", shown.Select(LibraryVersionNumber.Format))}, and a new version starts from the newest, {LibraryVersionNumber.Format(number)}; UpdatePlcLibraryInstances moves it there first");
        }

        /// <summary>Throws when an instance about to move shows a version in work: it is that version's test instance.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="softwarePath">The program, named in a refusal.</param>
        /// <param name="released">The type's released versions.</param>
        /// <param name="instanceVersions">The version each instance about to move shows.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the version in work.</exception>
        /// <remarks>
        /// Moving it would delete the instance the version in work is edited through and put a released version in its place,
        /// and putting it back would need creating an instance from a version in work, which TIA Portal creates only from a
        /// released one. So the version in work is released or discarded first.
        /// </remarks>
        public static void RequireNoneInWork(string typeName, string softwarePath, IReadOnlyCollection<Version> released, IReadOnlyList<Version> instanceVersions)
        {
            if (released == null || instanceVersions == null)
            {
                throw new ArgumentNullException(released == null ? nameof(released) : nameof(instanceVersions));
            }

            var releasedNumbers = released.Select(LibraryVersionNumber.Normalise).ToList();
            var inWork = instanceVersions.Select(LibraryVersionNumber.Normalise).Where(version => !releasedNumbers.Contains(version)).Distinct().ToList();
            if (inWork.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The instance of '{typeName}' in '{softwarePath}' shows {string.Join(", ", inWork.Select(LibraryVersionNumber.Format))}, which is in work: it is the instance that version is edited through. Release it (ReleaseLibraryType) or discard it first. Nothing was changed");
            }
        }
    }
}
