using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>Reads the alarm classes of an alarm list.</summary>
    /// <remarks>
    /// The shape of one class, every field but the name optional:
    /// <code>
    /// { "name": "Tolva", "priority": 10, "stateMachine": "raiseClearRequiresAcknowledgement",
    ///   "states": { "raised": { "backColor": "255, 128, 0", "textColor": "black", "flashing": true },
    ///               "acknowledged": { "backColor": "yellow" } } }
    /// </code>
    /// </remarks>
    internal static class UnifiedAlarmClassReader
    {
        private static readonly string[] ClassFields = { "name", "priority", "stateMachine", "states" };

        private static readonly string[] VisualFields = { "backColor", "textColor", "flashing" };

        private static readonly Regex WithAlpha = new Regex(@"^\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*$", RegexOptions.CultureInvariant);

        /// <summary>Reads every class.</summary>
        /// <param name="root">The list's JSON object.</param>
        /// <returns>The classes, none when the list has no 'classes'.</returns>
        public static IReadOnlyList<UnifiedAlarmClassDescription> Read(JsonElement root)
        {
            if (!root.TryGetProperty("classes", out var classes) || classes.ValueKind == JsonValueKind.Null)
            {
                return Array.Empty<UnifiedAlarmClassDescription>();
            }

            if (classes.ValueKind != JsonValueKind.Array)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'classes' must be a list of alarm classes");
            }

            var read = classes.EnumerateArray().Select((element, index) => ReadClass(element, index + 1)).ToList();
            var repeated = read.GroupBy(found => found.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? read
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Alarm class names are unique, in any case; repeated: {string.Join(", ", repeated)}");
        }

        private static UnifiedAlarmClassDescription ReadClass(JsonElement element, int position)
        {
            var owner = UnifiedTagJson.RequireObject(element, $"Alarm class {position}");
            var name = UnifiedAlarmJson.RequireName(owner, $"Alarm class {position}");
            var what = $"Alarm class '{name}'";
            UnifiedTagJson.RequireKnownFields(owner, ClassFields, what);

            return new UnifiedAlarmClassDescription(
                name,
                UnifiedAlarmJson.OptionalPriority(owner, what),
                UnifiedAlarmJson.OptionalEnum<UnifiedAlarmStateMachine>(owner, "stateMachine", what),
                ReadStates(owner, what));
        }

        private static Dictionary<UnifiedAlarmState, UnifiedAlarmStateVisuals> ReadStates(JsonElement owner, string what)
        {
            var states = new Dictionary<UnifiedAlarmState, UnifiedAlarmStateVisuals>();
            if (!owner.TryGetProperty("states", out var element) || element.ValueKind == JsonValueKind.Null)
            {
                return states;
            }

            var known = Enum.GetNames(typeof(UnifiedAlarmState)).Select(UnifiedAlarmJson.Camel).ToArray();
            var given = UnifiedTagJson.RequireObject(element, $"{what}: 'states'");
            UnifiedTagJson.RequireKnownFields(given, known, $"{what}: 'states'");

            foreach (var state in given.EnumerateObject())
            {
                states[(UnifiedAlarmState)Enum.Parse(typeof(UnifiedAlarmState), state.Name, ignoreCase: true)] = ReadVisuals(state.Value, $"{what}, state '{state.Name}'");
            }

            return states;
        }

        private static UnifiedAlarmStateVisuals ReadVisuals(JsonElement element, string what)
        {
            var visuals = UnifiedTagJson.RequireObject(element, what);
            UnifiedTagJson.RequireKnownFields(visuals, VisualFields, what);

            return new UnifiedAlarmStateVisuals(
                Colour(visuals, "backColor", what),
                Colour(visuals, "textColor", what),
                UnifiedTagJson.OptionalBool(visuals, "flashing"));
        }

        private static string? Colour(JsonElement visuals, string field, string what)
        {
            var given = UnifiedTagJson.OptionalString(visuals, field);
            if (given == null)
            {
                return null;
            }

            // R, G, B, A: a colour that is not opaque, as GetUnifiedAlarms reads one back.
            var withAlpha = WithAlpha.Match(given);
            if (!withAlpha.Success)
            {
                return HmiColor.Parse(given, $"{what}, {field}");
            }

            var channels = Enumerable.Range(1, 4).Select(group => int.Parse(withAlpha.Groups[group].Value, CultureInfo.InvariantCulture)).ToArray();

            return channels.All(channel => channel <= byte.MaxValue)
                ? string.Join(", ", channels)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}, {field}: channels run from 0 to 255; got '{given}'");
        }
    }
}
