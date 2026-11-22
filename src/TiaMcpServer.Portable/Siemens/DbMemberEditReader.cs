using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a change to some members of a global data block from JSON, refusing one that is incomplete or ambiguous.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "block": "DADES COMPARTIR",
    ///   "remove": [ "Old" ],
    ///   "set": [ { "name": "Count", "startValue": 9 } ],
    ///   "add": [ { "name": "Extra", "dataType": "Int", "startValue": 7 } ] }
    /// </code>
    /// A member is named in one list at most; a member added needs a data type; a member set gives at least one of data type
    /// and start value. Members of the block's own level only: a struct's members are not edited here.
    /// </remarks>
    public static class DbMemberEditReader
    {
        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly Regex MemberName = new Regex(@"^[\p{L}_][\p{L}\p{N}_ ]{0,127}$", RegexOptions.CultureInvariant);

        private static readonly Regex DataTypeText = new Regex(@"^[^\p{C}<>&]{1,200}$", RegexOptions.CultureInvariant);

        private static readonly Regex StartValueText = new Regex(@"^[^\p{C}]{1,500}$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> RowFields = new HashSet<string>(StringComparer.Ordinal) { "name", "dataType", "startValue" };

        /// <summary>Reads an edit.</summary>
        /// <param name="json">The edit.</param>
        /// <returns>The edit to apply.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid, the edit empty, a row incomplete or a member named twice.</exception>
        public static DbMemberEdit Parse(string json)
        {
            var root = ParseObject(json);
            var block = Text(root, "block") ?? throw new PortalException(PortalErrorCode.InvalidParams, "The edit needs 'block': the data block's path, as GetBlocks prints it");

            var remove = List(root, "remove").Select(ReadName).ToList();
            var set = List(root, "set").Select(entry => ReadRow(entry, "set", added: false)).ToList();
            var add = List(root, "add").Select(entry => ReadRow(entry, "add", added: true)).ToList();

            var edit = new DbMemberEdit(block, remove, (set, add));
            RequireSomething(edit);
            RequireEachNamedOnce(edit);

            return edit;
        }

        private static string ReadName(JsonNode? entry)
        {
            var name = entry is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null;

            return name != null && MemberName.IsMatch(name)
                ? name
                : throw new PortalException(PortalErrorCode.InvalidParams, "'remove' lists member names, as strings: letters, digits, underscores and spaces, not starting with a digit");
        }

        private static DbMemberRow ReadRow(JsonNode? entry, string list, bool added)
        {
            var row = entry as JsonObject ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Each entry of '{list}' must be an object: {{ \"name\": ..., ... }}");
            var unknown = row.Select(field => field.Key).FirstOrDefault(key => !RowFields.Contains(key));
            if (unknown != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"An entry of '{list}' has '{unknown}', which a member does not take here; it takes {string.Join(", ", RowFields)}");
            }

            var name = ReadName(row["name"] ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Each entry of '{list}' needs the member's 'name'"));
            var read = new DbMemberRow(name, DataType(row, name), StartValue(row, name));

            if (added && read.DataType == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' is added, so it needs a 'dataType', e.g. \"Int\" or \"String[10]\"");
            }

            return added || read.DataType != null || read.StartValue != null
                ? read
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'set' for '{name}' changes nothing: give 'dataType' or 'startValue'");
        }

        private static string? DataType(JsonObject row, string name)
        {
            var dataType = Text(row, "dataType");

            return dataType == null || DataTypeText.IsMatch(dataType)
                ? dataType
                : throw new PortalException(PortalErrorCode.InvalidParams, $"The data type of '{name}' is not text TIA Portal can take: up to 200 characters, none of < > & or control characters");
        }

        /// <remarks>
        /// A number or a boolean is taken as TIA Portal writes it (<c>9</c>, <c>21.5</c>, <c>true</c>); a string is taken as it
        /// stands, so a String member's start value carries its quotes: <c>"'abc'"</c>.
        /// </remarks>
        private static string? StartValue(JsonObject row, string name)
        {
            var text = row["startValue"] switch
            {
                null => null,
                JsonValue value when value.TryGetValue<string>(out var given) => given,
                JsonValue value when value.TryGetValue<bool>(out var flag) => flag ? "true" : "false",
                JsonValue value when value.TryGetValue<double>(out var number) => number.ToString("R", CultureInfo.InvariantCulture),
                _ => throw new PortalException(PortalErrorCode.InvalidParams, $"The start value of '{name}' must be a string, a number or true/false")
            };

            return text == null || StartValueText.IsMatch(text)
                ? text
                : throw new PortalException(PortalErrorCode.InvalidParams, $"The start value of '{name}' is empty, too long (500 characters) or holds control characters");
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

        private static string? Text(JsonObject owner, string key)
        {
            return owner[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
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

        private static void RequireSomething(DbMemberEdit edit)
        {
            if (edit.Remove.Count == 0 && edit.Set.Count == 0 && edit.Add.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The edit changes nothing: give 'remove', 'set' or 'add'");
            }
        }

        /// <remarks>Member names are compared case aside, as TIA Portal compares them.</remarks>
        private static void RequireEachNamedOnce(DbMemberEdit edit)
        {
            var repeated = edit.Remove.Concat(edit.Set.Select(row => row.Name)).Concat(edit.Add.Select(row => row.Name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"A member is named once in an edit, in 'remove', 'set' or 'add'; named more than once: {string.Join(", ", repeated)}");
            }
        }
    }
}
