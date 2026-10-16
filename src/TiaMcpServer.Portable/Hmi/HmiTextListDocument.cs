using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Hmi
{
    /// <summary>
    /// Reads a text list from JSON and writes it as the XML ImportHmiTextList takes: one text per value
    /// of a tag, which a symbolic I/O field shows.
    /// </summary>
    /// <remarks>
    /// The shape is the class projects' value lists (2026-10-13): range <c>Decimal</c>, an empty
    /// comment, and per value an entry of type <c>SingleValue</c> whose from and to are that value.
    /// Value ranges and bit lists exist in TIA Portal but were not in any export, so they are not
    /// written.
    /// <code>
    /// { "name": "MODES", "entries": [ { "value": 0, "text": "STOPPED" }, { "value": 1, "text": "AUTOMATIC" } ] }
    /// </code>
    /// </remarks>
    public static class HmiTextListDocument
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Reads a text list description and builds its XML.</summary>
        /// <param name="json">The description.</param>
        /// <returns>The list's name and its XML.</returns>
        /// <exception cref="PortalException">The JSON is invalid, or the list has no name, no entries, or a repeated value.</exception>
        public static KeyValuePair<string, string> Build(string json)
        {
            var document = Deserialise(json);
            var name = (document.Name ?? string.Empty).Trim();
            var entries = document.Entries ?? new List<EntryDocument>();

            if (name.Length == 0 || entries.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A text list needs a 'name' and at least one entry");
            }

            if (entries.Any(entry => entry.Value == null || string.IsNullOrWhiteSpace(entry.Text)))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Every text list entry needs a 'value' and a 'text'");
            }

            if (entries.GroupBy(entry => entry.Value).Any(group => group.Count() > 1))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Each value may appear once in a text list");
            }

            return new KeyValuePair<string, string>(name, Write(name, entries));
        }

        private static ListDocument Deserialise(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ListDocument>(json ?? string.Empty, Options)
                    ?? throw new PortalException(PortalErrorCode.InvalidParams, "The text list description is empty");
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The text list description is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static string Write(string name, IReadOnlyList<EntryDocument> entries)
        {
            var writer = new HmiScreenWriter();
            var list = writer.Node("Hmi.TextGraphicList.TextList", string.Empty, () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string> { ["ListRange"] = "Decimal", ["Name"] = name }),
                HmiScreenWriter.Objects(new[] { writer.EmptyText("Comment") }
                    .Concat(entries.Select(entry => WriteEntry(writer, entry.Value!.Value, entry.Text!.Trim())))
                    .ToList())
            });

            return HmiXmlText.Serialise(new XDocument(new XElement("Document", new XElement("Engineering", new XAttribute("version", "V20")), list)));
        }

        private static XElement WriteEntry(HmiScreenWriter writer, long value, string text)
        {
            var number = HmiScreenWriter.Number(value);

            return writer.Node("Hmi.TextGraphicList.TextListEntry", "Entries", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string>
                {
                    ["DefaultEntry"] = "false",
                    ["EntryType"] = "SingleValue",
                    ["From"] = number,
                    ["To"] = number
                }),
                HmiScreenWriter.Objects(new[] { writer.Text("Text", text) })
            });
        }

        // CA1812 reports these as never instantiated, and it cannot see otherwise: the only thing that
        // creates them is System.Text.Json, by reflection.
#pragma warning disable CA1812

        private sealed class ListDocument
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("entries")]
            public List<EntryDocument>? Entries { get; set; }
        }

        private sealed class EntryDocument
        {
            [JsonPropertyName("value")]
            public long? Value { get; set; }

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

#pragma warning restore CA1812
    }
}
