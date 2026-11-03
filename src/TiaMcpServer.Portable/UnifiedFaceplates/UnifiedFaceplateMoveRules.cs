using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>What a faceplate instance must keep when it moves to another version of its type.</summary>
    /// <remarks>
    /// Measured 2026-11-01 on TIA Portal V20: a move keeps what an interface was given when the version has the same
    /// property. What a version that renames or drops one does was not measured, as Openness cannot change a faceplate's
    /// interface to try it; so what an interface held is compared, and a difference refused rather than reported as moved.
    /// </remarks>
    public static class UnifiedFaceplateMoveRules
    {
        /// <summary>Throws when an interface property that held a value holds another after the move, or is gone.</summary>
        /// <param name="faceplate">The instance, named in the refusal.</param>
        /// <param name="reference">The version moved to, named in the refusal.</param>
        /// <param name="before">Each property's name and value as text, before the move.</param>
        /// <param name="after">The same, after it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming what would be lost.</exception>
        /// <remarks>A property that held nothing has nothing to lose, and a property the version adds is no loss.</remarks>
        public static void CheckKept(string faceplate, string reference, IReadOnlyList<(string Name, string Value)> before, IReadOnlyList<(string Name, string Value)> after)
        {
            var kept = after.ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
            var lost = before
                .Where(property => !string.IsNullOrEmpty(property.Value))
                .Where(property => !kept.TryGetValue(property.Name, out var value) || !string.Equals(value, property.Value, StringComparison.Ordinal))
                .Select(property => $"'{property.Name}' (held '{property.Value}')")
                .ToList();

            if (lost.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Moving faceplate '{faceplate}' to {reference} would lose what its interface held: {string.Join(", ", lost)}. That version changed its interface; give the values again with the instance in TIA Portal.");
            }
        }
    }
}
