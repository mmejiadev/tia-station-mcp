using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a WinCC Unified screen into the shape <see cref="UnifiedScreenDescriptionReader"/> takes.</summary>
    /// <remarks>
    /// A Unified screen has no export in Openness V20 — <c>HmiScreen</c> offers none — so this is the
    /// backup taken before a screen is redrawn, and drawing what it wrote puts the screen back. Only what
    /// a description can set is read: writable values, texts, parts, points and tag links. A property that
    /// throws when read is named under <c>unreadable</c> rather than left out without a word.
    /// </remarks>
    internal sealed class UnifiedScreenReader
    {
        private readonly Language _language;
        private readonly List<string> _unreadable = new List<string>();

        /// <summary>Creates a reader.</summary>
        /// <param name="language">The project's editing language, which texts are read in.</param>
        public UnifiedScreenReader(Language language)
        {
            _language = language;
        }

        /// <summary>Reads a screen.</summary>
        /// <param name="screen">The screen.</param>
        /// <param name="startScreen">The runtime's start screen, to say whether this is it.</param>
        /// <returns>The screen as a description.</returns>
        public JsonObject Read(HmiScreen screen, string? startScreen)
        {
            var items = new JsonArray();

            foreach (var item in screen.ScreenItems)
            {
                items.Add(ReadItem(item));
            }

            var read = new JsonObject
            {
                ["name"] = screen.Name,
                ["startScreen"] = string.Equals(startScreen, screen.Name, StringComparison.OrdinalIgnoreCase),
                ["properties"] = ReadProperties(screen),
                ["items"] = items
            };

            if (_unreadable.Count > 0)
            {
                read["unreadable"] = new JsonArray(_unreadable.Select(path => (JsonNode)JsonValue.Create(path)!).ToArray());
            }

            return read;
        }

        private JsonObject ReadItem(HmiScreenItemBase item)
        {
            var read = new JsonObject
            {
                ["type"] = UnifiedItemTypes.ShortName(item.GetType().FullName!),
                ["name"] = item.Name,
                ["properties"] = ReadProperties(item)
            };

            if (item.GetType().GetProperty("Points")?.GetValue(item) is HmiPointComposition points)
            {
                read["points"] = new JsonArray(points.Select(point => (JsonNode)new JsonArray(point.X, point.Y)).ToArray());
            }

            var tags = new JsonObject();
            ReadTags(item, string.Empty, tags, 0);
            if (tags.Count > 0)
            {
                read["tags"] = tags;
            }

            if (UnifiedEventWriter.Read(item) is JsonArray events)
            {
                read["events"] = events;
            }

            return read;
        }

        private JsonObject ReadProperties(object owner)
        {
            var properties = new JsonObject();
            ReadProperties(owner, string.Empty, properties, 0);

            return properties;
        }

        private void ReadProperties(object owner, string prefix, JsonObject into, int depth)
        {
            foreach (var property in Readable(owner.GetType()))
            {
                var path = prefix + property.Name;
                if (!TryRead(property, owner, path, out var value) || value == null)
                {
                    continue;
                }

                if (UnifiedPropertyTree.IsPart(property.PropertyType) && depth < UnifiedPropertyTree.MaxPartDepth)
                {
                    ReadProperties(value, path + ".", into, depth + 1);
                    continue;
                }

                var node = Describe(property, value);
                if (node != null)
                {
                    into[path] = node;
                }
            }
        }

        private JsonNode? Describe(PropertyInfo property, object value)
        {
            if (value is MultilingualText text)
            {
                var stored = text.Items.Find(_language)?.Text;

                return stored == null ? null : UnifiedRichText.Read(stored);
            }

            var described = UnifiedPropertyWriter.SetterRefusal(property) == null ? UnifiedPropertyConverter.Describe(value) : null;

            return described.HasValue ? JsonValue.Create(described.Value) : null;
        }

        private void ReadTags(object owner, string prefix, JsonObject into, int depth)
        {
            if (owner.GetType().GetProperty("Dynamizations")?.GetValue(owner) is DynamizationBaseComposition dynamizations)
            {
                foreach (var tag in dynamizations.OfType<TagDynamization>())
                {
                    into[prefix + tag.PropertyName] = tag.Tag;
                }
            }

            if (depth >= UnifiedPropertyTree.MaxPartDepth)
            {
                return;
            }

            foreach (var part in Readable(owner.GetType()).Where(property => UnifiedPropertyTree.IsPart(property.PropertyType)))
            {
                if (TryRead(part, owner, prefix + part.Name, out var value) && value != null)
                {
                    ReadTags(value, prefix + part.Name + ".", into, depth + 1);
                }
            }
        }

        private bool TryRead(PropertyInfo property, object owner, string path, out object? value)
        {
            try
            {
                value = property.GetValue(owner);
                return true;
            }
            catch (TargetInvocationException failure)
            {
                _unreadable.Add($"{path}: {failure.InnerException?.Message ?? failure.Message}");
                value = null;
                return false;
            }
        }

        private static IEnumerable<PropertyInfo> Readable(Type type)
        {
            return type.GetProperties()
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && !UnifiedPropertyTree.IsNotAProperty(property.Name))
                .OrderBy(property => property.Name, StringComparer.Ordinal);
        }
    }
}
