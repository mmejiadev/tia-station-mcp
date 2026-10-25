using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Reads a WinCC Unified screen description from JSON, refusing one that is incomplete.</summary>
    /// <remarks>
    /// The shape — the same one <c>GetUnifiedScreen</c> prints, so a screen read can be drawn again:
    /// <code>
    /// { "name": "Main", "startScreen": true,
    ///   "properties": { "Width": 800, "Height": 480, "BackColor": "#202020" },
    ///   "items": [
    ///     { "type": "rectangle", "name": "Tank", "properties": { "Left": 40, "Top": 60, "Width": 120, "Height": 200, "BackColor": "blue" } },
    ///     { "type": "polyline", "name": "Pipe", "points": [[0, 0], [60, 0], [60, 90]], "properties": { "LineWidth": 4 } },
    ///     { "type": "ioField", "name": "Level", "properties": { "Left": 200, "Top": 60, "Font.Size": 14 }, "tags": { "ProcessValue": "Level" } },
    ///     { "type": "text", "name": "Title", "properties": { "Text": "Tank 1" } } ] }
    /// </code>
    /// Property names are Openness's own; <c>GetUnifiedItemTypes</c> lists them for every type. An item
    /// without a name gets <c>&lt;type&gt;_&lt;n&gt;</c>. Comments and trailing commas are allowed.
    /// </remarks>
    public static class UnifiedScreenDescriptionReader
    {
        private const int MinimumPoints = 2;

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads a description.</summary>
        /// <param name="json">The description.</param>
        /// <returns>The screen to draw.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid or the description incomplete.</exception>
        public static UnifiedScreenDescription Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = RequireObject(document.RootElement, "The screen description");

            var name = OptionalString(root, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The screen needs a 'name'");
            }

            var isStartScreen = root.TryGetProperty("startScreen", out var start) && start.ValueKind == JsonValueKind.True;
            var items = ReadItems(root);

            return new UnifiedScreenDescription(name!.Trim(), isStartScreen, ReadProperties(root, $"Screen '{name}'"), items);
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The screen description is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static List<UnifiedItemDescription> ReadItems(JsonElement root)
        {
            if (!root.TryGetProperty("items", out var items) || items.ValueKind == JsonValueKind.Null)
            {
                return new List<UnifiedItemDescription>();
            }

            if (items.ValueKind != JsonValueKind.Array)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'items' must be a list");
            }

            var read = items.EnumerateArray().Select((item, index) => ReadItem(item, index + 1)).ToList();
            RequireUniqueNames(read);

            return read;
        }

        private static UnifiedItemDescription ReadItem(JsonElement element, int position)
        {
            var item = RequireObject(element, $"Item {position}");
            var typeName = UnifiedItemTypes.Resolve(OptionalString(item, "type") ?? string.Empty);
            var name = OptionalString(item, "name")?.Trim();
            var itemName = string.IsNullOrEmpty(name) ? $"{UnifiedItemTypes.ShortName(typeName)}_{position}" : name!;
            var what = $"Item '{itemName}'";

            var drawing = new UnifiedItemDrawing(ReadPoints(item, typeName, what), ReadTags(item, what), UnifiedEventListReader.Read(item, what));

            return new UnifiedItemDescription(typeName, itemName, ReadProperties(item, what), drawing);
        }

        private static List<UnifiedPropertySetting> ReadProperties(JsonElement owner, string what)
        {
            if (!owner.TryGetProperty("properties", out var properties) || properties.ValueKind == JsonValueKind.Null)
            {
                return new List<UnifiedPropertySetting>();
            }

            var settings = RequireObject(properties, $"{what}: 'properties'").EnumerateObject()
                .Select(property => new UnifiedPropertySetting(property.Name, property.Value))
                .ToList();

            var repeated = settings.GroupBy(setting => setting.Path, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? settings
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} sets a property twice: {string.Join(", ", repeated)}");
        }

        private static List<UnifiedPoint> ReadPoints(JsonElement item, string typeName, string what)
        {
            var hasPoints = item.TryGetProperty("points", out var points) && points.ValueKind != JsonValueKind.Null;
            var isPointBased = UnifiedItemTypes.IsPointBased(typeName);

            if (hasPoints != isPointBased)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    isPointBased ? $"{what} is a polyline or a polygon and needs 'points'" : $"{what} takes no 'points': only a polyline or a polygon does");
            }

            if (!hasPoints)
            {
                return new List<UnifiedPoint>();
            }

            var read = points.ValueKind == JsonValueKind.Array ? points.EnumerateArray().Select(point => ReadPoint(point, what)).ToList() : null;

            return read != null && read.Count >= MinimumPoints
                ? read
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'points' must list at least two points, as [[x, y], [x, y]]");
        }

        private static UnifiedPoint ReadPoint(JsonElement point, string what)
        {
            var coordinates = point.ValueKind == JsonValueKind.Array ? point.EnumerateArray().ToList() : new List<JsonElement>();

            if (coordinates.Count == 2 && coordinates.All(coordinate => coordinate.ValueKind == JsonValueKind.Number && coordinate.TryGetInt32(out _)))
            {
                return new UnifiedPoint(coordinates[0].GetInt32(), coordinates[1].GetInt32());
            }

            throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a point is written [x, y] with whole numbers; got {point.GetRawText()}");
        }

        private static Dictionary<string, string> ReadTags(JsonElement item, string what)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!item.TryGetProperty("tags", out var links) || links.ValueKind == JsonValueKind.Null)
            {
                return tags;
            }

            foreach (var link in RequireObject(links, $"{what}: 'tags'").EnumerateObject())
            {
                var tag = link.Value.ValueKind == JsonValueKind.String ? link.Value.GetString() : null;
                if (string.IsNullOrWhiteSpace(tag))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: the tag for '{link.Name}' must be an HMI tag's name");
                }

                tags[UnifiedPropertySetting.NormalisePath(link.Name)] = tag!.Trim();
            }

            return tags;
        }

        private static void RequireUniqueNames(IReadOnlyList<UnifiedItemDescription> items)
        {
            var repeated = items.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Item names must be unique on a screen; repeated: {string.Join(", ", repeated)}");
            }
        }

        private static JsonElement RequireObject(JsonElement element, string what)
        {
            return element.ValueKind == JsonValueKind.Object
                ? element
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
        }

        private static string? OptionalString(JsonElement owner, string name)
        {
            return owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}
