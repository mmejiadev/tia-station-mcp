using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>Which types may be brought from one library into another: copied into the project library, or published to a global one.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: copying a type the project library already holds does not
    /// fail. It adds the global library's versions and renumbers the project's version in work without a
    /// word (<c>0.0.1</c> in work became <c>0.0.2</c> in work). So a type already in the project is
    /// refused, never merged. The types a copied one depends on (a faceplate's PLC data type) come with
    /// it on their own, so they are refused alike; a name the global library does not hold is refused
    /// here, as TIA Portal answers it only with "The selection of types and folders contained a null".
    /// </remarks>
    public static class LibraryCopyRules
    {
        /// <summary>The project library, as a refusal names it.</summary>
        public const string ProjectLibrary = "project library";

        /// <summary>A global library, as a refusal names it.</summary>
        public const string GlobalLibrary = "global library";

        /// <summary>Throws unless the names asked for can be looked up in the library they come from.</summary>
        /// <param name="requested">The type names asked for.</param>
        /// <param name="held">Every type name that library holds.</param>
        /// <param name="library">That library, as the refusal names it: <see cref="ProjectLibrary"/> or <see cref="GlobalLibrary"/>.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when none is named, one is named twice or the library lacks one.</exception>
        /// <remarks>Names are compared case aside, as TIA Portal keeps one type per name whatever its case.</remarks>
        public static void CheckRequested(IReadOnlyList<string> requested, IReadOnlyCollection<string> held, string library)
        {
            if (requested == null || requested.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Name at least one type");
            }

            var repeated = requested.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Type named twice: '{string.Join("', '", repeated)}'");
            }

            var missing = requested.Where(name => !held.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The {library} has no type '{string.Join("', '", missing)}'. It holds: {string.Join(", ", held)}");
            }
        }

        /// <summary>Throws when the library receiving types already holds one that would be brought, asked for or depended on.</summary>
        /// <param name="brought">Every type brought: those asked for and every type they depend on, transitively.</param>
        /// <param name="held">Every type name the receiving library holds.</param>
        /// <param name="library">That library, as the refusal names it: <see cref="ProjectLibrary"/> or <see cref="GlobalLibrary"/>.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the types already there.</exception>
        /// <remarks>
        /// A dependency is brought along without being asked for, and merged into one already there just as
        /// silently, so it is checked like the types named. Only the copy into a project library was measured to
        /// merge; a global library is refused alike, as it is the one often shared and then saved.
        /// </remarks>
        public static void CheckNotPresent(IReadOnlyCollection<string> brought, IReadOnlyCollection<string> held, string library)
        {
            var present = (brought ?? Array.Empty<string>()).Where(name => held.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();

            if (present.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The {library} already holds '{string.Join("', '", present)}', which this would bring; nothing was changed. "
                    + "Bringing a type over one already there can renumber its version in work without a word, so update it in TIA Portal instead.");
            }
        }
    }
}
