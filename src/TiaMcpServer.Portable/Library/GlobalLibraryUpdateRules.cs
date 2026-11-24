using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>Which new versions of a type a global library that already holds it may be given from the project library.</summary>
    /// <remarks>
    /// Measured 2026-11-24 on TIA Portal V21: <c>ProjectLibrary.UpdateLibrary</c> on a type the global library already holds
    /// adds the project's released versions it lacks, keeping each one's GUID, and makes the newest the default there; a version
    /// in work is not taken. What it does to a global library whose versions went another way - one the project has never seen -
    /// was not measured, so that is refused: every version the global library holds must be one of the project's, by GUID.
    /// </remarks>
    public static class GlobalLibraryUpdateRules
    {
        /// <summary>The versions a type would bring to a global library that holds it already.</summary>
        /// <param name="typeName">The type, named in a refusal.</param>
        /// <param name="project">Every version the project library holds of it.</param>
        /// <param name="global">Every version the global library holds of it, or null when it does not hold the type.</param>
        /// <returns>The released versions the global library lacks, oldest first.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when the global library lacks the type, holds a version the project does not,
        /// or already holds every released version.
        /// </exception>
        public static IReadOnlyList<Version> RequireNewVersions(string typeName, IReadOnlyList<LibraryVersionIdentity> project, IReadOnlyList<LibraryVersionIdentity>? global)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (global == null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The global library does not hold '{typeName}' yet; PublishLibraryTypes publishes it the first time");
            }

            var foreign = global.Where(version => !project.Any(own => own.Id == version.Id)).ToList();
            if (foreign.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The global library holds {Describe(foreign)} of '{typeName}', which the project library does not: the two went different ways, and what TIA Portal does with that was not measured. Nothing was written");
            }

            var added = project.Where(own => own.IsReleased && !global.Any(version => version.Id == own.Id)).Select(own => own.Number).OrderBy(number => number).ToList();

            return added.Count > 0
                ? added
                : throw new PortalException(PortalErrorCode.InvalidState, $"The global library already holds every released version of '{typeName}' ({Describe(global)}); ReleaseLibraryType releases a version in work first");
        }

        /// <summary>Throws unless every version the new versions depend on is in the global library, or arrives with them.</summary>
        /// <param name="typeName">The type whose new versions these are, named in a refusal.</param>
        /// <param name="needed">Each version a new version depends on: its type, number and GUID.</param>
        /// <param name="available">The GUIDs of every version the global library holds, and of every version this call adds.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the versions missing.</exception>
        /// <remarks>
        /// <c>UpdateLibrary</c> brings what a type depends on along with it, and what that does to a dependency the global library
        /// holds in other versions was not measured; so a dependency is taken only when it is already there or named in the call.
        /// </remarks>
        public static void RequireDependenciesPresent(string typeName, IEnumerable<(string Type, Version Number, Guid Id)> needed, ICollection<Guid> available)
        {
            if (needed == null || available == null)
            {
                throw new ArgumentNullException(needed == null ? nameof(needed) : nameof(available));
            }

            var missing = needed.Where(dependency => !available.Contains(dependency.Id)).Select(dependency => $"{dependency.Type} {LibraryVersionNumber.Format(dependency.Number)}").Distinct().ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The new versions of '{typeName}' depend on {string.Join(", ", missing)}, which the global library does not hold; name those types in the same call, or publish them first. Nothing was written");
            }
        }

        /// <summary>Throws when a version that was to be added is not in the global library afterwards.</summary>
        /// <param name="typeName">The type, named in the failure.</param>
        /// <param name="expected">The versions that were to arrive.</param>
        /// <param name="after">The global library's versions of the type, read back.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.WriteFailed"/> naming the versions missing.</exception>
        public static void RequireArrived(string typeName, IReadOnlyList<Version> expected, IReadOnlyList<LibraryVersionIdentity> after)
        {
            if (expected == null || after == null)
            {
                throw new ArgumentNullException(expected == null ? nameof(expected) : nameof(after));
            }

            var missing = expected.Where(number => !after.Any(version => version.Number == LibraryVersionNumber.Normalise(number))).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal did not bring {string.Join(", ", missing.Select(LibraryVersionNumber.Format))} of '{typeName}' to the global library and gave no error; the library was left as it was");
            }
        }

        private static string Describe(IEnumerable<LibraryVersionIdentity> versions)
        {
            return string.Join(", ", versions.OrderBy(version => version.Number).Select(version => LibraryVersionNumber.Format(version.Number)));
        }
    }
}
