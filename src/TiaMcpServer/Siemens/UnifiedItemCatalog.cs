using Siemens.Engineering;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>What a description may set on each WinCC Unified item type, read from Openness itself.</summary>
    /// <remarks>
    /// Read from the types, not written down: whatever this TIA Portal's Openness offers is what is
    /// listed, so the list cannot fall behind the installation. A property is <c>settable</c> when the
    /// writer can set it, and otherwise carries the reason it cannot, so nothing is left out unexplained.
    /// </remarks>
    internal static class UnifiedItemCatalog
    {
        /// <summary>Describes one item type.</summary>
        /// <param name="name">The item type, as a description names it.</param>
        /// <returns>The type and every property path, with its value type and whether it can be set.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the name is not a drawable item.</exception>
        public static JsonObject Describe(string name)
        {
            var fullName = UnifiedItemTypes.Resolve(name);
            var properties = new JsonArray();

            AddProperties(UnifiedScreenItemWriter.ResolveType(fullName), string.Empty, properties, 0);

            return new JsonObject
            {
                ["type"] = UnifiedItemTypes.ShortName(fullName),
                ["openness"] = fullName,
                ["pointBased"] = UnifiedItemTypes.IsPointBased(fullName),
                ["properties"] = properties
            };
        }

        private static void AddProperties(Type type, string prefix, JsonArray into, int depth)
        {
            // The points are listed although a description gives them in their own field: this is the
            // list that says where each property is set.
            var listed = type.GetProperties()
                .Where(p => p.Name == UnifiedPropertyTree.Points || !UnifiedPropertyTree.IsNotAProperty(p.Name))
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            foreach (var property in listed)
            {
                var path = prefix + property.Name;

                if (UnifiedPropertyTree.IsPart(property.PropertyType) && depth < UnifiedPropertyTree.MaxPartDepth)
                {
                    AddProperties(property.PropertyType, path + ".", into, depth + 1);
                    continue;
                }

                into.Add(DescribeProperty(property, path));
            }
        }

        private static JsonObject DescribeProperty(PropertyInfo property, string path)
        {
            var type = property.PropertyType;
            var described = new JsonObject
            {
                ["path"] = path,
                ["valueType"] = type.Name,
                ["settable"] = RefusalFor(property) == null
            };

            if (RefusalFor(property) is string refusal)
            {
                described["why"] = refusal;
            }

            if (type.IsEnum)
            {
                described["values"] = new JsonArray(Enum.GetNames(type).Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());
            }

            return described;
        }

        private static string? RefusalFor(PropertyInfo property)
        {
            var type = property.PropertyType;

            if (type == typeof(MultilingualText))
            {
                return null;
            }

            if (type.Name == "HmiPointComposition")
            {
                return "set through the item's 'points'";
            }

            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                return "a list of parts (thresholds, selection items, trend areas): not drawn by this tool yet";
            }

            if (!UnifiedPropertyConverter.Supports(type))
            {
                return $"a {type.Name}, which this tool does not set";
            }

            return UnifiedPropertyWriter.SetterRefusal(property);
        }
    }
}
