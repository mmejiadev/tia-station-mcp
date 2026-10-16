using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Hmi
{
    /// <summary>Reads a screen description from JSON, refusing one that is incomplete.</summary>
    /// <remarks>
    /// The shape, with every item field optional unless its kind needs it:
    /// <code>
    /// { "name": "principal", "number": 2, "backColor": "white",
    ///   "items": [
    ///     { "type": "button", "left": 116, "top": 49, "text": "Marxa", "press": "setBitWhilePressed", "tag": "Marxa" },
    ///     { "type": "button", "left": 541, "top": 37, "text": "Reset", "press": "setTag", "tag": "CONT1", "value": 0 },
    ///     { "type": "button", "left": 20, "top": 360, "text": "Grafics", "press": "activateScreen", "screen": "Grafics" },
    ///     { "type": "ioField", "left": 327, "top": 20, "tag": "CONT1", "mode": "InOutput", "format": "9999" },
    ///     { "type": "text", "left": 348, "top": 239, "text": "M1" },
    ///     { "type": "pilotLight", "left": 335, "top": 142, "tag": "KM1", "offColor": "red", "onColor": "green" },
    ///     { "type": "bar", "left": 696, "top": 16, "tag": "Level", "minimum": 0, "maximum": 1000 } ] }
    /// </code>
    /// Any item takes <c>name</c>, <c>width</c>, <c>height</c>, <c>backColor</c>, <c>foreColor</c>,
    /// <c>fontSize</c> and <c>align</c> (Left, Center, Right); a pilot light also <c>flashing</c> and
    /// <c>visibleTag</c>, and its <c>backColor</c> is its colour before the animation applies. Comments and trailing
    /// commas are allowed.
    /// </remarks>
    public static class HmiScreenDescriptionReader
    {
        private const string DefaultBackColor = "255, 255, 255";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Reads a description.</summary>
        /// <param name="json">The description.</param>
        /// <returns>The screen to generate.</returns>
        /// <exception cref="PortalException">The JSON is invalid, or the description incomplete.</exception>
        public static HmiScreenDescription Parse(string json)
        {
            var document = Deserialise(json);

            if (string.IsNullOrWhiteSpace(document.Name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The screen needs a 'name': it is what buttons navigate to");
            }

            var backColor = HmiColor.Parse(document.BackColor ?? DefaultBackColor, "The screen's backColor");
            var items = ReadItems(document.Items ?? new List<HmiScreenItemDocument>());

            return new HmiScreenDescription(document.Name!.Trim(), document.Number, backColor, items);
        }

        private static ScreenDocument Deserialise(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ScreenDocument>(json ?? string.Empty, Options)
                    ?? throw new PortalException(PortalErrorCode.InvalidParams, "The screen description is empty");
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The screen description is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static List<HmiScreenItem> ReadItems(IReadOnlyList<HmiScreenItemDocument> documents)
        {
            var factory = new HmiScreenItemFactory();
            var items = documents.Select((item, index) => factory.Create(item, index + 1)).ToList();

            var repeated = items.GroupBy(item => item.Frame.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            return repeated.Count == 0
                ? items
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Item names must be unique on a screen; repeated: {string.Join(", ", repeated)}");
        }

        // CA1812 reports this as never instantiated, and it cannot see otherwise: the only thing that
        // creates it is System.Text.Json, by reflection.
#pragma warning disable CA1812

        /// <summary>The description's shape, and nothing more.</summary>
        private sealed class ScreenDocument
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("number")]
            public int? Number { get; set; }

            [JsonPropertyName("backColor")]
            public string? BackColor { get; set; }

            [JsonPropertyName("items")]
            public List<HmiScreenItemDocument>? Items { get; set; }
        }

#pragma warning restore CA1812
    }
}
