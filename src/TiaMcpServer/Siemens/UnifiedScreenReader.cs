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
    /// a description can set is read: writable values, texts, parts, points, tag links, events and
    /// animations. A property that
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

            var tags = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var animations = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
            ReadDynamizations(item, string.Empty, (tags, animations), 0);
            if (tags.Count > 0)
            {
                read["tags"] = new JsonObject(tags.Select(tag => new KeyValuePair<string, JsonNode?>(tag.Key, tag.Value)));
            }

            if (UnifiedEventWriter.Read(item) is JsonArray events)
            {
                read["events"] = events;
            }

            if (animations.Count > 0)
            {
                read["animations"] = new JsonArray(animations.Values.Cast<JsonNode>().ToArray());
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

        /// <summary>Every dynamization of an item and of its parts.</summary>
        /// <param name="owner">The item.</param>
        /// <returns>The dynamizations, the item's own first.</returns>
        /// <remarks>A part that throws when read ends the call here, before anything was deleted.</remarks>
        public static IReadOnlyList<DynamizationBase> Dynamizations(object owner)
        {
            var found = new List<DynamizationBase>();
            CollectDynamizations(owner, found, 0);

            return found;
        }

        private static void CollectDynamizations(object owner, List<DynamizationBase> found, int depth)
        {
            if (owner.GetType().GetProperty(UnifiedScreenItemWriter.DynamizationsProperty)?.GetValue(owner) is DynamizationBaseComposition dynamizations)
            {
                found.AddRange(dynamizations);
            }

            if (depth >= UnifiedPropertyTree.MaxPartDepth)
            {
                return;
            }

            foreach (var part in Readable(owner.GetType()).Where(property => UnifiedPropertyTree.IsPart(property.PropertyType)))
            {
                if (part.GetValue(owner) is object value)
                {
                    CollectDynamizations(value, found, depth + 1);
                }
            }
        }

        /// <remarks>
        /// A plain tag link is written under <c>tags</c>; a range table, a flashing colour or a script under
        /// <c>animations</c>. Before 2026-10-25 every tag link went under <c>tags</c>, so a range table
        /// was backed up as a plain link and lost on a redraw without a word. Both are kept in the order of
        /// their paths: Openness lists an item's dynamizations in an order of its own, not the order they
        /// were created in (measured 2026-10-25), and a screen read twice must read the same.
        /// </remarks>
        private void ReadDynamizations(object owner, string prefix, (SortedDictionary<string, string> Tags, SortedDictionary<string, JsonObject> Animations) into, int depth)
        {
            if (owner.GetType().GetProperty(UnifiedScreenItemWriter.DynamizationsProperty)?.GetValue(owner) is DynamizationBaseComposition dynamizations)
            {
                foreach (var dynamization in dynamizations)
                {
                    AddDynamization(dynamization, prefix + dynamization.PropertyName, into);
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
                    ReadDynamizations(value, prefix + part.Name + ".", into, depth + 1);
                }
            }
        }

        private static void AddDynamization(DynamizationBase dynamization, string path, (SortedDictionary<string, string> Tags, SortedDictionary<string, JsonObject> Animations) into)
        {
            if (dynamization is TagDynamization link && UnifiedDynamizationCoverage.IsPlainLink(link))
            {
                into.Tags[path] = link.Tag;
                return;
            }

            if (UnifiedAnimationReader.Read(dynamization, path) is JsonObject animation)
            {
                into.Animations[path] = animation;
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
