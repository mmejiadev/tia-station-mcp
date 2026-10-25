using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Reads an item's <c>events</c> into one script per event.</summary>
    /// <remarks>
    /// Two ways to write an event, for each entry of the list:
    /// <code>
    /// "events": [
    ///   { "event": "down", "function": "setBit", "tag": "Motor" },
    ///   { "event": "up", "function": "resetBit", "tag": "Motor" },
    ///   { "event": "tapped", "function": "changeScreen", "screen": "Recipes", "window": "../Screen window_3" },
    ///   { "event": "keyDown", "script": "Tags(\"Level\").Write(0);" } ]
    /// </code>
    /// Functions written for the same event run in the order given, as one script; an event with its
    /// own <c>script</c> takes nothing else, since mixing the two would leave an order nobody wrote. Which
    /// events an item has is checked against its Openness type when it is drawn.
    /// </remarks>
    public static class UnifiedEventListReader
    {
        private static readonly Regex EventName = new Regex("^[A-Za-z]+$", RegexOptions.CultureInvariant);

        private static readonly string[] ScriptOnly = { "globalDefinitions" };

        private static readonly string[] FunctionOnly = { "tag", "value", "screen", "window" };

        private static readonly HashSet<string> Fields = new HashSet<string>(StringComparer.Ordinal)
        {
            "event", "script", "globalDefinitions", "function", "tag", "value", "screen", "window"
        };

        /// <summary>Reads the events of an item, which it may have none of.</summary>
        /// <param name="item">The item's JSON.</param>
        /// <param name="what">The item, as a refusal names it.</param>
        /// <returns>One script per event, in the order the events first appear.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static IReadOnlyList<UnifiedEventScript> Read(JsonElement item, string what)
        {
            if (!item.TryGetProperty("events", out var events) || events.ValueKind == JsonValueKind.Null)
            {
                return Array.Empty<UnifiedEventScript>();
            }

            if (events.ValueKind != JsonValueKind.Array)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'events' must be a list");
            }

            return events.EnumerateArray()
                .Select((entry, index) => (Entry: entry, What: $"{what}, event {index + 1}"))
                .GroupBy(pair => RequireEvent(pair.Entry, pair.What), StringComparer.OrdinalIgnoreCase)
                .Select(group => Combine(group.Key, group.ToList(), what))
                .ToList();
        }

        private static UnifiedEventScript Combine(string eventName, IReadOnlyList<(JsonElement Entry, string What)> entries, string what)
        {
            var scripts = entries.Where(pair => pair.Entry.TryGetProperty("script", out _)).ToList();

            if (scripts.Count == 0)
            {
                RequireNone(entries, ScriptOnly, "belongs to an event given as a 'script'");
                return new UnifiedEventScript(eventName, string.Join(Environment.NewLine, entries.Select(pair => UnifiedEventFunctions.Write(pair.Entry, pair.What))), null);
            }

            if (entries.Count > 1 || scripts[0].Entry.TryGetProperty("function", out _))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: the '{eventName}' event has its own 'script', so it takes no other entry; put everything in the script, or use functions only");
            }

            var entry = scripts[0].Entry;
            var script = entry.GetProperty("script");
            if (script.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(script.GetString()))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{scripts[0].What}: 'script' must be JavaScript, as text");
            }

            RequireNone(scripts, FunctionOnly, "is an argument of a 'function', not of a 'script'");
            var globals = ReadGlobals(entry, scripts[0].What);

            return new UnifiedEventScript(eventName, script.GetString()!, globals);
        }

        private static void RequireNone(IEnumerable<(JsonElement Entry, string What)> entries, string[] fields, string why)
        {
            foreach (var (entry, what) in entries)
            {
                var given = fields.FirstOrDefault(field => entry.TryGetProperty(field, out _));
                if (given != null)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{given}' {why}, so it would be lost");
                }
            }
        }

        private static string? ReadGlobals(JsonElement entry, string what)
        {
            if (!entry.TryGetProperty("globalDefinitions", out var global) || global.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return global.ValueKind == JsonValueKind.String
                ? global.GetString()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'globalDefinitions' must be JavaScript, as text");
        }

        private static string RequireEvent(JsonElement entry, string what)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
            }

            var unknown = entry.EnumerateObject().Select(property => property.Name).Where(name => !Fields.Contains(name)).ToList();
            if (unknown.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has fields this tool does not set: {string.Join(", ", unknown)}");
            }

            var name = entry.TryGetProperty("event", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;

            return EventName.IsMatch(name)
                ? name
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'event' is required, an event of the item such as 'down', 'up' or 'tapped'");
        }
    }
}
