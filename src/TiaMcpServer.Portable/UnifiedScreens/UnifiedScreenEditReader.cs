using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Reads a change to some items of a WinCC Unified screen from JSON, refusing one that is incomplete or ambiguous.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "screen": "Main",
    ///   "properties": { "BackColor": "#202020" },
    ///   "remove": [ "Old_Button" ],
    ///   "set": [ { "name": "Level", "properties": { "Left": 300, "Font.Size": 16 } } ],
    ///   "draw": [ { "type": "button", "name": "Start", "properties": { "Left": 40, "Top": 400, "Text": "Start" } } ] }
    /// </code>
    /// An item in <c>draw</c> is written exactly as in <c>DrawUnifiedScreen</c> - it goes through the same reader - but
    /// must be named, as it is added, or replaces the item of that name. An item is named in one list at most: what
    /// happens to it is then one thing, not an order to guess. <c>set</c> takes properties only; links, events and
    /// animations change by drawing the item again.
    /// </remarks>
    public static class UnifiedScreenEditReader
    {
        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly string[] DrawnOnly = { "type", "tags", "events", "animations", "points" };

        /// <summary>Reads an edit.</summary>
        /// <param name="json">The edit.</param>
        /// <returns>The edit to apply.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid, the edit empty, or an item named twice.</exception>
        public static UnifiedScreenEdit Parse(string json)
        {
            var root = ParseObject(json);
            var screen = root["screen"] is JsonValue name && name.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : throw new PortalException(PortalErrorCode.InvalidParams, "The edit needs 'screen': the name of a screen that exists");

            var properties = Described(new JsonObject { ["name"] = screen, ["properties"] = root["properties"]?.DeepClone() }).Properties;
            var edit = new UnifiedScreenEdit(screen, properties, new UnifiedItemChanges(ReadRemove(root), ReadSet(root), ReadDraw(root, screen)));

            RequireSomething(edit);
            RequireEachNamedOnce(edit);

            return edit;
        }

        private static JsonObject ParseObject(string json)
        {
            try
            {
                return JsonNode.Parse(json ?? string.Empty, null, Options) as JsonObject
                    ?? throw new PortalException(PortalErrorCode.InvalidParams, "The edit must be a JSON object");
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The edit is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static List<string> ReadRemove(JsonObject root)
        {
            return List(root, "remove").Select(entry => entry is JsonValue value && value.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name.Trim()
                    : throw new PortalException(PortalErrorCode.InvalidParams, "'remove' lists item names, as strings"))
                .ToList();
        }

        private static List<UnifiedItemSettings> ReadSet(JsonObject root)
        {
            return List(root, "set").Select(ReadSettings).ToList();
        }

        private static UnifiedItemSettings ReadSettings(JsonNode? entry)
        {
            var item = entry as JsonObject ?? throw new PortalException(PortalErrorCode.InvalidParams, "Each entry of 'set' must be an object: { \"name\": ..., \"properties\": { ... } }");
            var name = item["name"] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : throw new PortalException(PortalErrorCode.InvalidParams, "Each entry of 'set' needs the 'name' of an item on the screen");

            var drawnOnly = DrawnOnly.FirstOrDefault(item.ContainsKey);
            if (drawnOnly != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'set' changes properties only, and '{name}' gives '{drawnOnly}': draw the item again in 'draw' to change its type, links, events, animations or points");
            }

            var properties = Described(new JsonObject { ["name"] = name, ["properties"] = item["properties"]?.DeepClone() }).Properties;

            return properties.Count > 0
                ? new UnifiedItemSettings(name, properties)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'set' for '{name}' gives no properties");
        }

        private static IReadOnlyList<UnifiedItemDescription> ReadDraw(JsonObject root, string screen)
        {
            var entries = List(root, "draw");
            // Review of 2026-11-17: a blank name became the reader's 'Button_1', and replaced an item of that name unasked.
            var unnamed = entries.Select((entry, index) => (entry, index)).Where(pair => !HasName(pair.entry)).Select(pair => pair.index + 1).ToList();
            if (unnamed.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Item {unnamed[0]} of 'draw' needs a 'name': it is added under it, or replaces the item of that name");
            }

            return Described(new JsonObject { ["name"] = screen, ["items"] = new JsonArray(entries.Select(entry => entry!.DeepClone()).ToArray()) }).Items;
        }

        private static bool HasName(JsonNode? entry)
        {
            return entry is JsonObject item && item["name"] is JsonValue value && value.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name);
        }

        /// <remarks>Through the drawing's own reader, so a property, an item or a link is read exactly as <c>DrawUnifiedScreen</c> reads it.</remarks>
        private static UnifiedScreenDescription Described(JsonObject description)
        {
            return UnifiedScreenDescriptionReader.Parse(description.ToJsonString());
        }

        private static IReadOnlyList<JsonNode?> List(JsonObject root, string key)
        {
            return root[key] switch
            {
                null => Array.Empty<JsonNode?>(),
                JsonArray list => list.ToList(),
                _ => throw new PortalException(PortalErrorCode.InvalidParams, $"'{key}' must be a list")
            };
        }

        private static void RequireSomething(UnifiedScreenEdit edit)
        {
            if (edit.Properties.Count == 0 && edit.Remove.Count == 0 && edit.Set.Count == 0 && edit.Draw.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The edit changes nothing: give 'properties', 'remove', 'set' or 'draw'");
            }
        }

        private static void RequireEachNamedOnce(UnifiedScreenEdit edit)
        {
            var repeated = edit.Remove
                .Concat(edit.Set.Select(item => item.Name))
                .Concat(edit.Draw.Select(item => item.Name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"An item is named once in an edit, in 'remove', 'set' or 'draw'; named more than once: {string.Join(", ", repeated)}");
            }
        }
    }
}
