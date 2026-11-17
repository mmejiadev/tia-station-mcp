using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Reads which Custom Web Controls to place on a screen, from JSON.</summary>
    /// <remarks>
    /// The shape: <c>{ screen, controls: [ { name, control, left, top, width, height, properties: { Label: "M1" },
    /// tags: { IsOn: "M1.KM" }, events: { Clicked: "script" } } ] }</c>. A property is given a fixed value or a tag, not
    /// both: TIA Portal would keep only one of them, and which one is not something to leave to chance.
    /// </remarks>
    public static class CustomWebControlPlacementReader
    {
        private const int MaximumItemNameLength = 128;

        /// <summary>Reads the list.</summary>
        /// <param name="json">The list as JSON text.</param>
        /// <returns>The checked list.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming the first part that is wrong.</exception>
        public static CustomWebControlPlacementList Read(string json)
        {
            using var document = Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("controls", out var controls) || controls.ValueKind != JsonValueKind.Array)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Give { screen, controls: [ { name, control, left, top, properties, tags, events } ] }");
            }

            var screen = Text(root, "screen");
            if (screen.Length == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "screen is required");
            }

            var placements = controls.EnumerateArray().Select(ReadPlacement).ToList();
            RequireDistinctNames(placements);

            return new CustomWebControlPlacementList(screen, placements);
        }

        private static CustomWebControlPlacement ReadPlacement(JsonElement item)
        {
            var name = Text(item, "name").Trim();
            if (name.Length == 0 || name.Length > MaximumItemNameLength)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Each control needs a name of 1 to {MaximumItemNameLength} characters; got '{name}'");
            }

            var values = Members(item, "properties", name).ToDictionary(member => member.Name, member => member.Value.Clone(), StringComparer.Ordinal);
            var tags = Members(item, "tags", name).ToDictionary(member => member.Name, member => RequireText(member.Value, $"Tag of '{name}.{member.Name}'"), StringComparer.Ordinal);
            var events = Members(item, "events", name).ToDictionary(member => member.Name, member => RequireText(member.Value, $"Script of '{name}.{member.Name}'"), StringComparer.Ordinal);

            var both = values.Keys.Intersect(tags.Keys, StringComparer.Ordinal).FirstOrDefault();
            if (both != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}.{both}' is given a value and a tag; give it one");
            }

            var frame = new UnifiedFaceplateFrame(Integer(item, "left") ?? 0, Integer(item, "top") ?? 0, Size(item, "width"), Size(item, "height"));

            return new CustomWebControlPlacement(name, RequireInstalledName(Text(item, "control"), name), frame, new CustomWebControlInterface(values, tags, events));
        }

        /// <remarks>
        /// Not this server's naming rule: a control installed by somebody else may be named otherwise, and the writer looks it
        /// up among those installed, which is the check that matters.
        /// </remarks>
        private static string RequireInstalledName(string control, string item)
        {
            var trimmed = control.Trim();

            return trimmed.Length > 0 && trimmed.Length <= MaximumItemNameLength && !trimmed.Any(char.IsControl)
                ? trimmed
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{item}' needs a control: the name GetCustomWebControls lists, e.g. 'TiaStation.Motor'");
        }

        private static void RequireDistinctNames(IReadOnlyList<CustomWebControlPlacement> placements)
        {
            var repeated = placements.GroupBy(placement => placement.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

            if (repeated != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{repeated.Key}' is placed twice; each screen item needs its own name");
            }
        }

        private static JsonDocument Parse(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty);
            }
            catch (JsonException failure)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The list is not JSON: {failure.Message}", null, failure);
            }
        }

        /// <remarks>JSON allows a name twice in one object; which of the two was meant is not guessed.</remarks>
        private static IEnumerable<JsonProperty> Members(JsonElement owner, string name, string item)
        {
            if (!owner.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return Enumerable.Empty<JsonProperty>();
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' must be an object of name to value");
            }

            var members = value.EnumerateObject().ToList();
            var repeated = members.GroupBy(member => member.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);

            return repeated == null
                ? members
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{item}' names '{repeated.Key}' twice in '{name}'");
        }

        private static string Text(JsonElement owner, string name)
        {
            return owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
        }

        private static string RequireText(JsonElement value, string what)
        {
            return value.ValueKind == JsonValueKind.String && value.GetString()!.Trim().Length > 0
                ? value.GetString()!
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a non-empty text");
        }

        private static int? Integer(JsonElement owner, string name)
        {
            if (!owner.TryGetProperty(name, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' must be a whole number of pixels");
        }

        private static uint? Size(JsonElement owner, string name)
        {
            var size = Integer(owner, name);

            return size == null ? null : (uint)CustomWebControlRules.RequireSize(size.Value, name);
        }
    }
}
