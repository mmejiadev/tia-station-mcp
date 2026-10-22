using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>One property to set, by its path through the item's parts, and the value as written.</summary>
    /// <remarks>
    /// A path is the property's name, or a part and its property: <c>BackColor</c>, <c>Font.Size</c>,
    /// <c>Caption.Font.Weight</c>. Its type is only known against the Openness type, so the value is kept
    /// as the JSON it came in and converted when the item is drawn.
    /// </remarks>
    public sealed class UnifiedPropertySetting
    {
        private static readonly Regex Segment = new Regex("^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);

        /// <summary>Creates a setting.</summary>
        /// <param name="path">The property's path.</param>
        /// <param name="value">The value, as JSON.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the path is not one a description may set.</exception>
        public UnifiedPropertySetting(string path, JsonElement value)
        {
            Segments = RequirePath(path);
            Path = string.Join(".", Segments);
            Value = value.Clone();
        }

        /// <summary>The path, e.g. <c>Font.Size</c>.</summary>
        public string Path { get; }

        /// <summary>The path's segments, the property last.</summary>
        public IReadOnlyList<string> Segments { get; }

        /// <summary>The value, as JSON.</summary>
        public JsonElement Value { get; }

        /// <summary>Checks a property path and spells it without stray spaces.</summary>
        /// <param name="path">The path, e.g. <c>Font.Size</c>.</param>
        /// <returns>The path as it will be applied.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is not one a description may name.</exception>
        public static string NormalisePath(string path)
        {
            return string.Join(".", RequirePath(path));
        }

        private static List<string> RequirePath(string path)
        {
            var segments = (path ?? string.Empty).Split('.').Select(segment => segment.Trim()).ToList();

            if (segments.Count > UnifiedPropertyTree.MaxSegments || segments.Any(segment => !Segment.IsMatch(segment)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' is not a property path; write 'BackColor' or 'Font.Size'");
            }

            if (UnifiedPropertyTree.IsNotAProperty(segments[0]))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{segments[0]}' is not set through properties: use the item's 'name', 'points' or 'tags'");
            }

            return segments;
        }
    }
}
