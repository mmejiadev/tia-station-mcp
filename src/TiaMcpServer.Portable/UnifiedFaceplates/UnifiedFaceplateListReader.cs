using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedAlarms;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>Reads the faceplate instances to place on a WinCC Unified screen from JSON.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "screen": "Principal",
    ///   "faceplates": [ { "name": "FP_M1", "type": "FACEPLATE_MOTOR_V1", "version": "0.0.1",
    ///                     "left": 20, "top": 40, "width": 250, "height": 250,
    ///                     "interface": { "Motor": { "tag": "M1" }, "Nom": "Motor 1" } } ] }
    /// </code>
    /// A fixed value is a string: a text property was measured to take plain text and wrap it itself
    /// (2026-10-30); other kinds of fixed value were not measured, so they are not taken.
    /// </remarks>
    public static class UnifiedFaceplateListReader
    {
        private static readonly string[] ListFields = { "screen", "faceplates" };

        private static readonly string[] FaceplateFields = { "name", "type", "version", "left", "top", "width", "height", "interface" };

        private static readonly string[] TagFields = { "tag" };

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads a faceplate list.</summary>
        /// <param name="json">The list.</param>
        /// <returns>The screen and the instances to place on it.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid or an entry incomplete.</exception>
        public static UnifiedFaceplateList Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = UnifiedTagJson.RequireObject(document.RootElement, "The faceplate list");
            UnifiedTagJson.RequireKnownFields(root, ListFields, "The faceplate list");

            var screen = UnifiedTagJson.OptionalString(root, "screen");
            if (string.IsNullOrWhiteSpace(screen))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The faceplate list needs 'screen', the screen to place them on; DrawUnifiedScreen makes one");
            }

            if (!root.TryGetProperty("faceplates", out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'faceplates' must be a list of at least one faceplate");
            }

            var faceplates = entries.EnumerateArray().Select((entry, index) => ReadFaceplate(entry, index + 1)).ToList();
            RequireUnique(faceplates.Select(faceplate => faceplate.Name));

            return new UnifiedFaceplateList(screen!.Trim(), faceplates);
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The faceplate list is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static UnifiedFaceplateDescription ReadFaceplate(JsonElement element, int position)
        {
            var owner = UnifiedTagJson.RequireObject(element, $"Faceplate {position}");
            var name = UnifiedAlarmJson.RequireName(owner, $"Faceplate {position}");
            var what = $"Faceplate '{name}'";
            UnifiedTagJson.RequireKnownFields(owner, FaceplateFields, what);

            var type = UnifiedTagJson.OptionalString(owner, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs 'type', the faceplate's library type; GetLibraryTypes lists them");
            }

            var version = UnifiedTagJson.OptionalString(owner, "version");
            var parsedVersion = version == null ? null : LibraryVersionNumber.Parse(version, $"{what}: 'version'");
            var frame = new UnifiedFaceplateFrame(RequireInt(owner, "left", what), RequireInt(owner, "top", what), OptionalSize(owner, "width", what), OptionalSize(owner, "height", what));

            return new UnifiedFaceplateDescription(name, (type!.Trim(), parsedVersion), frame, ReadInterface(owner, what));
        }

        private static Dictionary<string, UnifiedFaceplateInterfaceValue> ReadInterface(JsonElement owner, string what)
        {
            // Case aside: a property named twice, in any case, would be written twice and the last one would win.
            var values = new Dictionary<string, UnifiedFaceplateInterfaceValue>(StringComparer.OrdinalIgnoreCase);
            if (!owner.TryGetProperty("interface", out var entries) || entries.ValueKind == JsonValueKind.Null)
            {
                return values;
            }

            foreach (var property in UnifiedTagJson.RequireObject(entries, $"{what}: 'interface'").EnumerateObject())
            {
                if (values.ContainsKey(property.Name))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: interface property '{property.Name}' is given twice");
                }

                values.Add(property.Name, ReadInterfaceValue(property.Value, $"{what}: interface property '{property.Name}'"));
            }

            return values;
        }

        private static UnifiedFaceplateInterfaceValue ReadInterfaceValue(JsonElement value, string what)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                return UnifiedFaceplateInterfaceValue.Fixed(value.GetString()!);
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be {{\"tag\": \"M1\"}} for an HMI tag, or a string for a fixed text; got {value.GetRawText()}");
            }

            UnifiedTagJson.RequireKnownFields(value, TagFields, what);
            var tag = UnifiedTagJson.OptionalString(value, "tag");

            return string.IsNullOrWhiteSpace(tag)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs 'tag', the HMI tag it is given")
                : UnifiedFaceplateInterfaceValue.Tag(tag!.Trim());
        }

        private static int RequireInt(JsonElement owner, string field, string what)
        {
            return owner.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs '{field}', a whole number of pixels");
        }

        private static uint? OptionalSize(JsonElement owner, string field, string what)
        {
            if (!owner.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var size) && size > 0
                ? size
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' must be a whole number of pixels from 1; got {value.GetRawText()}");
        }

        private static void RequireUnique(IEnumerable<string> names)
        {
            var repeated = names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Faceplate named twice: '{string.Join("', '", repeated)}'");
            }
        }
    }
}
