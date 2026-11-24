using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a change to some rows of a PLC tag table from JSON, refusing one that is incomplete or ambiguous.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "table": "Default tag table",
    ///   "remove": [ "Old_Lamp" ],
    ///   "set": [ { "name": "Start", "address": "%I0.1", "comment": "Start button, NO" } ],
    ///   "add": [ { "name": "Stop", "dataType": "Bool", "address": "%I0.2", "comment": "Stop button, NC" } ] }
    /// </code>
    /// A tag is named in one list at most; a tag added needs a data type and an address; a tag set gives at least one of
    /// data type, address and comment. Only tags: constants are created by <c>CreateConstant</c>.
    /// </remarks>
    public static class TagTableEditReader
    {
        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly HashSet<string> RowFields = new HashSet<string>(StringComparer.Ordinal) { "name", "dataType", "address", "comment" };

        /// <summary>Reads an edit.</summary>
        /// <param name="json">The edit.</param>
        /// <returns>The edit to apply.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid, the edit empty, a row incomplete or a tag named twice.</exception>
        public static TagTableEdit Parse(string json)
        {
            var root = ParseObject(json);
            var table = Text(root, "table") ?? throw new PortalException(PortalErrorCode.InvalidParams, "The edit needs 'table': the tag table's path, as GetTagTables prints it");

            var remove = List(root, "remove").Select(entry => entry is JsonValue value && value.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name)
                ? name.Trim()
                : throw new PortalException(PortalErrorCode.InvalidParams, "'remove' lists tag names, as strings")).ToList();
            var set = List(root, "set").Select(entry => ReadRow(entry, "set", added: false)).ToList();
            var add = List(root, "add").Select(entry => ReadRow(entry, "add", added: true)).ToList();

            var edit = new TagTableEdit(table, remove, (set, add));
            RequireSomething(edit);
            RequireEachNamedOnce(edit);

            return edit;
        }

        private static TagTableRow ReadRow(JsonNode? entry, string list, bool added)
        {
            var row = entry as JsonObject ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Each entry of '{list}' must be an object: {{ \"name\": ..., ... }}");
            var unknown = row.Select(field => field.Key).FirstOrDefault(key => !RowFields.Contains(key));
            if (unknown != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"An entry of '{list}' has '{unknown}', which a tag does not take here; it takes {string.Join(", ", RowFields)}");
            }

            var name = Text(row, "name") ?? throw new PortalException(PortalErrorCode.InvalidParams, $"Each entry of '{list}' needs the tag's 'name'");
            var read = new TagTableRow(name, Text(row, "dataType"), Address(row, name), row["comment"] is JsonValue comment && comment.TryGetValue<string>(out var text) ? text : null);

            if (added && (read.DataTypeName == null || read.Address == null))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{name}' is added, so it needs a 'dataType' and an 'address', e.g. \"Bool\" and \"%I0.0\"");
            }

            return added || read.DataTypeName != null || read.Address != null || read.Comment != null
                ? read
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'set' for '{name}' changes nothing: give 'dataType', 'address' or 'comment'");
        }

        /// <remarks>TIA Portal takes any text as an address (measured), so <see cref="PlcTagAddress"/> checks it.</remarks>
        private static string? Address(JsonObject row, string name)
        {
            var address = Text(row, "address");

            return address == null ? null : PlcTagAddress.Parse(address, name);
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

        private static void RequireSomething(TagTableEdit edit)
        {
            if (edit.Remove.Count == 0 && edit.Set.Count == 0 && edit.Add.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The edit changes nothing: give 'remove', 'set' or 'add'");
            }
        }

        /// <remarks>Tag names are compared case aside, as TIA Portal compares them.</remarks>
        private static void RequireEachNamedOnce(TagTableEdit edit)
        {
            var repeated = edit.Remove.Concat(edit.Set.Select(row => row.Name)).Concat(edit.Add.Select(row => row.Name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"A tag is named once in an edit, in 'remove', 'set' or 'add'; named more than once: {string.Join(", ", repeated)}");
            }
        }
    }
}
