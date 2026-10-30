using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Library
{
    /// <summary>Which types may be copied from a global library into the project library.</summary>
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
        /// <summary>Throws unless the names asked for can be looked up in the global library.</summary>
        /// <param name="requested">The type names to copy.</param>
        /// <param name="inGlobalLibrary">Every type name the global library holds.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when none is named, one is named twice or the global library lacks one.</exception>
        /// <remarks>Names are compared case aside, as TIA Portal keeps one type per name whatever its case.</remarks>
        public static void CheckRequested(IReadOnlyList<string> requested, IReadOnlyCollection<string> inGlobalLibrary)
        {
            if (requested == null || requested.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Name at least one type to copy");
            }

            var repeated = requested.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Type named twice: '{string.Join("', '", repeated)}'");
            }

            var missing = requested.Where(name => !inGlobalLibrary.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The global library has no type '{string.Join("', '", missing)}'. It holds: {string.Join(", ", inGlobalLibrary)}");
            }
        }

        /// <summary>Throws when the project library already holds a type the copy would bring, asked for or depended on.</summary>
        /// <param name="brought">Every type the copy brings: those asked for and every type they depend on, transitively.</param>
        /// <param name="inProjectLibrary">Every type name the project library holds.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming the types already there.</exception>
        /// <remarks>
        /// A dependency is brought along without being asked for, and merged into one already there just as
        /// silently, so it is checked like the types named.
        /// </remarks>
        public static void CheckNotPresent(IReadOnlyCollection<string> brought, IReadOnlyCollection<string> inProjectLibrary)
        {
            var present = (brought ?? Array.Empty<string>()).Where(name => inProjectLibrary.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();

            if (present.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The project library already holds '{string.Join("', '", present)}', which this copy would bring; nothing was copied. "
                    + "Copying over a type already there would renumber its version in work, so update it in TIA Portal instead.");
            }
        }
    }
}
