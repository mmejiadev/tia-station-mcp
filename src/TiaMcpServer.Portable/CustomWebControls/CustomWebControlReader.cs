using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Reads a Custom Web Control's definition from JSON, checking every part before anything is packaged.</summary>
    /// <remarks>
    /// The shape: <c>{ name, displayName, description, width, height, properties: [ { name, type, default } ],
    /// events: [ { name, arguments: [ { name, type } ] } ], html, css, script }</c>. A colour default is written the way
    /// screens write colours ('0, 200, 0', '#00C800', 'green'); the manifest holds it as the ARGB number TIA Portal reads.
    /// </remarks>
    public static class CustomWebControlReader
    {
        /// <summary>Reads a definition.</summary>
        /// <param name="json">The definition as JSON text.</param>
        /// <returns>The checked definition.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming the first part that is wrong.</exception>
        public static CustomWebControlDefinition Read(string json)
        {
            using var document = Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A control definition is a JSON object: { name, displayName, properties, events, html, css, script, width, height }");
            }

            var identity = new CustomWebControlIdentity(
                CustomWebControlRules.RequireControlName(Text(root, "name")),
                Text(root, "displayName") is { Length: > 0 } shown ? shown : Text(root, "name"),
                Text(root, "description"));

            return new CustomWebControlDefinition(identity, ReadProperties(root), ReadEvents(root), ReadContent(root));
        }

        private static JsonDocument Parse(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty);
            }
            catch (JsonException failure)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The control definition is not JSON: {failure.Message}", null, failure);
            }
        }

        private static List<CustomWebControlProperty> ReadProperties(JsonElement root)
        {
            var properties = Items(root, "properties").Select(item => ReadProperty(item, "Property", true)).ToList();

            RequireUnique(properties.Select(property => property.Name), "property", CustomWebControlRules.MaximumProperties);

            return properties;
        }

        private static List<CustomWebControlEvent> ReadEvents(JsonElement root)
        {
            var events = Items(root, "events").Select(ReadEvent).ToList();

            RequireUnique(events.Select(fired => fired.Name), "event", CustomWebControlRules.MaximumEvents);

            return events;
        }

        /// <remarks>Arguments are keys of one object in the manifest: a name given twice would leave TIA Portal to pick one (review of 2026-11-16).</remarks>
        private static CustomWebControlEvent ReadEvent(JsonElement item)
        {
            var name = CustomWebControlRules.RequireIdentifier(Text(item, "name"), "Event");
            var arguments = Items(item, "arguments").Select(argument => ReadProperty(argument, "Event argument", false)).ToList();

            RequireUnique(arguments.Select(argument => argument.Name), $"argument of event '{name}'", CustomWebControlRules.MaximumProperties);

            return new CustomWebControlEvent(name, arguments);
        }

        private static CustomWebControlContent ReadContent(JsonElement root)
        {
            var script = CustomWebControlRules.RequireRender(CustomWebControlRules.RequireScript(Text(root, "script")));

            return new CustomWebControlContent(
                CustomWebControlRules.RequireHtml(Text(root, "html")),
                CustomWebControlRules.RequireCss(Text(root, "css")),
                script,
                (CustomWebControlRules.RequireSize(Integer(root, "width"), "width"), CustomWebControlRules.RequireSize(Integer(root, "height"), "height")));
        }

        private static CustomWebControlProperty ReadProperty(JsonElement item, string what, bool hasDefault)
        {
            var name = CustomWebControlRules.RequireIdentifier(Text(item, "name"), what);
            var kind = CustomWebControlPropertyKinds.Parse(Text(item, "type"), $"{what} '{name}'");
            var given = hasDefault && item.TryGetProperty("default", out var value) ? value : (JsonElement?)null;

            return new CustomWebControlProperty(name, kind, CustomWebControlDefaults.Check(kind, given, $"{what} '{name}'"));
        }

        private static void RequireUnique(IEnumerable<string> names, string what, int maximum)
        {
            var list = names.ToList();
            var repeated = list.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

            if (repeated != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The {what} '{repeated.Key}' is named twice; names are compared without case");
            }

            if (list.Count > maximum)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{list.Count} {what} entries; at most {maximum} are taken");
            }
        }

        private static IEnumerable<JsonElement> Items(JsonElement owner, string name)
        {
            if (!owner.TryGetProperty(name, out var list) || list.ValueKind == JsonValueKind.Null)
            {
                return Enumerable.Empty<JsonElement>();
            }

            return list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().ToList()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' must be a list");
        }

        private static string Text(JsonElement owner, string name)
        {
            if (!owner.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return string.Empty;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' must be a text");
        }

        private static int Integer(JsonElement owner, string name)
        {
            return owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' must be a whole number of pixels");
        }
    }
}
