using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>What must hold before faceplates are placed, checked without TIA Portal.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: an interface property takes any string, a tag that does not
    /// exist included, and only the compile says "The object ... does not exist". So every tag a list names
    /// is checked here. Whether a tag's type suits the property is not: an interface property does not say
    /// its type through Openness (only <c>PropertyName</c> and <c>Value</c>), and the compile reports a
    /// mismatch the same way. A version in work cannot be placed at all, so only released versions count.
    /// Names are compared case aside, as TIA Portal keeps one per name whatever its case, and written back
    /// as TIA Portal spells them. The screen itself is found before this is asked, by the portal.
    /// </remarks>
    public sealed class UnifiedFaceplateRules
    {
        private readonly UnifiedFaceplateState _state;

        /// <summary>Creates the rules over what TIA Portal holds.</summary>
        /// <param name="state">The screen's item names, the released versions and the tag names.</param>
        public UnifiedFaceplateRules(UnifiedFaceplateState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Throws unless every faceplate can be placed as described.</summary>
        /// <param name="list">The faceplates.</param>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when a type, a released version or a tag does not exist;
        /// <see cref="PortalErrorCode.InvalidState"/> when a name is taken on the screen.
        /// </exception>
        public void Check(UnifiedFaceplateList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var missing = list.Faceplates.SelectMany(Missing).ToList();
            if (missing.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Nothing was placed: {string.Join("; ", missing)}");
            }

            var taken = list.Faceplates.Where(faceplate => _state.ScreenItems.Contains(faceplate.Name, StringComparer.OrdinalIgnoreCase)).Select(faceplate => faceplate.Name).ToList();
            if (taken.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was placed: the screen '{list.Screen}' already has an item named '{string.Join("', '", taken)}'");
            }
        }

        /// <summary>The released version a faceplate shows: the one it names, or the newest.</summary>
        /// <param name="faceplate">The faceplate, checked by <see cref="Check"/>.</param>
        /// <returns>The version.</returns>
        public Version VersionOf(UnifiedFaceplateDescription faceplate)
        {
            return faceplate?.Version ?? _state.ReleasedVersions[faceplate!.TypeName].Max()!;
        }

        /// <summary>The type's name as TIA Portal spells it, which the container's reference must use.</summary>
        /// <param name="faceplate">The faceplate, checked by <see cref="Check"/>.</param>
        /// <returns>E.g. <c>FP_Motor</c> for a description that wrote <c>fp_motor</c>.</returns>
        public string TypeNameOf(UnifiedFaceplateDescription faceplate)
        {
            return _state.ReleasedVersions.Keys.First(name => string.Equals(name, faceplate?.TypeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The value to write: for a tag, the tag's name as TIA Portal spells it, with the member path given.</summary>
        /// <param name="value">An interface value, checked by <see cref="Check"/>.</param>
        /// <returns><c>M1.Ft</c> for <c>m1.Ft</c>; a fixed value as it is.</returns>
        /// <remarks>An interface property was not measured to find a tag case aside, so the spelling TIA Portal holds is the one written.</remarks>
        public string ValueOf(UnifiedFaceplateInterfaceValue value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var root = value.RootTag();
            if (root == null)
            {
                return value.Value;
            }

            return _state.Tags.First(tag => string.Equals(tag, root, StringComparison.OrdinalIgnoreCase)) + value.Value.Substring(root.Length);
        }

        private IEnumerable<string> Missing(UnifiedFaceplateDescription faceplate)
        {
            var what = $"faceplate '{faceplate.Name}'";

            if (TypeProblem(faceplate) is string typeProblem)
            {
                yield return $"{what}: {typeProblem}";
            }

            foreach (var entry in faceplate.Interface.Where(entry => entry.Value.IsTag && !_state.Tags.Contains(entry.Value.RootTag()!, StringComparer.OrdinalIgnoreCase)))
            {
                yield return $"{what}: interface property '{entry.Key}' names the HMI tag '{entry.Value.Value}', which does not exist";
            }
        }

        private string? TypeProblem(UnifiedFaceplateDescription faceplate)
        {
            if (!_state.ReleasedVersions.TryGetValue(faceplate.TypeName, out var released))
            {
                return $"the project library has no type '{faceplate.TypeName}'; CopyLibraryTypes brings one from a global library";
            }

            if (released.Count == 0)
            {
                return $"'{faceplate.TypeName}' has no released version, and a version in work cannot be placed; ReleaseLibraryType releases it";
            }

            return faceplate.Version == null || released.Contains(faceplate.Version)
                ? null
                : $"'{faceplate.TypeName}' has no released version {LibraryVersionNumber.Format(faceplate.Version)}; released: {string.Join(", ", released.OrderBy(version => version).Select(LibraryVersionNumber.Format))}";
        }
    }
}
