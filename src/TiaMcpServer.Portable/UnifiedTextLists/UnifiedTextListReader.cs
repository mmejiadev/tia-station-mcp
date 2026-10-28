using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTextLists
{
    /// <summary>Reads a WinCC Unified text list from JSON, refusing what TIA Portal would take and lose.</summary>
    /// <remarks>
    /// The shape is the classic <c>GenerateHmiTextList</c>'s, so both tools are written alike:
    /// <code>{ "name": "States", "entries": [ { "value": 0, "text": "Stopped" }, { "value": 1, "text": "Running" } ] }</code>
    /// One value per entry. TIA Portal V20 imports a list holding a range of values (from 10 to 20) as a
    /// list with no entries at all, and says it succeeded (measured 2026-10-25), so ranges are not
    /// offered until one made in the editor has been measured. A name is a YAML key in the file TIA
    /// Portal imports, so it is kept to letters, digits and underscores; a text may hold any character
    /// but a line break, which was not measured.
    /// </remarks>
    public static class UnifiedTextListReader
    {
        /// <summary>The longest name, as for HMI tags and tables.</summary>
        public const int MaxNameLength = 128;

        private static readonly Regex Name = new Regex("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>Words a YAML reader takes for null or a boolean, not a name, written as the plain key a list is.</summary>
        private static readonly HashSet<string> YamlWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "null", "true", "false", "yes", "no", "on", "off", "y", "n"
        };

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly string[] ListFields = { "name", "entries" };

        private static readonly string[] EntryFields = { "value", "text" };

        /// <summary>Reads a text list.</summary>
        /// <param name="json">The list as JSON.</param>
        /// <returns>The list, its entries in the order given.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static UnifiedTextList Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The text list must be a JSON object: { \"name\": ..., \"entries\": [...] }");
            }

            RequireOnly(root, ListFields, "The text list");
            var name = RequireName(root);
            var entries = ReadEntries(root, $"Text list '{name}'");

            return new UnifiedTextList(name, entries);
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The text list is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static string RequireName(JsonElement root)
        {
            var name = root.TryGetProperty("name", out var given) && given.ValueKind == JsonValueKind.String ? given.GetString()!.Trim() : string.Empty;

            if (YamlWords.Contains(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' cannot name a text list: the file TIA Portal imports would read it as null or a boolean");
            }

            return Name.IsMatch(name) && name.Length <= MaxNameLength
                ? name
                : throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"A text list needs a 'name' of up to {MaxNameLength} letters, digits and underscores, starting with a letter; got '{name}'");
        }

        private static List<UnifiedTextListEntry> ReadEntries(JsonElement root, string what)
        {
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs 'entries', at least one: [{{ \"value\": 0, \"text\": \"Stopped\" }}]");
            }

            var read = entries.EnumerateArray().Select((entry, index) => ReadEntry(entry, $"{what}, entry {index + 1}")).ToList();
            var repeated = read.GroupBy(entry => entry.From).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? read
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} gives the value {string.Join(", ", repeated)} more than once");
        }

        /// <remarks>A value is a 32-bit integer: the range measured was -5 to 100000, and a list's tag is an integer.</remarks>
        private static UnifiedTextListEntry ReadEntry(JsonElement entry, string what)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
            }

            RequireOnly(entry, EntryFields, what);
            var value = entry.TryGetProperty("value", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole)
                ? whole
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'value' is required, a whole number from {int.MinValue} to {int.MaxValue}");

            var text = entry.TryGetProperty("text", out var given) && given.ValueKind == JsonValueKind.String ? given.GetString()!.Trim() : string.Empty;
            if (text.Length == 0 || text.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'text' is required, on one line");
            }

            return new UnifiedTextListEntry(value, value, text);
        }

        private static void RequireOnly(JsonElement owner, IReadOnlyCollection<string> fields, string what)
        {
            var stray = owner.EnumerateObject().Select(property => property.Name).Where(name => !fields.Contains(name, StringComparer.Ordinal)).ToList();

            if (stray.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has fields this tool does not set: {string.Join(", ", stray)}");
            }
        }
    }
}
