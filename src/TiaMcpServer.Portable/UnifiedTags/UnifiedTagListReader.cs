using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>Reads the WinCC Unified HMI tags to create from JSON, refusing a list that is incomplete.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "newTables": [ "Motors" ],
    ///   "tags": [
    ///     { "name": "Speed_SP", "table": "Motors", "dataType": "Int" },
    ///     { "name": "Motor_Run", "connection": "HMI_Connection_1", "plcTag": "Motor_Run" } ] }
    /// </code>
    /// A tag is internal, with a <c>dataType</c>, or linked, with a <c>connection</c> and a
    /// <c>plcTag</c>; never both, because a linked tag takes its type from the PLC tag (measured
    /// 2026-10-22: TIA Portal fills it in). A field nobody reads is refused rather than ignored.
    /// </remarks>
    public static class UnifiedTagListReader
    {
        private static readonly HashSet<string> TagFields = new HashSet<string>(StringComparer.Ordinal) { "name", "table", "dataType", "connection", "plcTag" };

        private static readonly HashSet<string> ListFields = new HashSet<string>(StringComparer.Ordinal) { "newTables", "tags" };

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads a tag list.</summary>
        /// <param name="json">The list.</param>
        /// <returns>The tables and tags to create.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid or a tag incomplete.</exception>
        public static UnifiedTagList Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = RequireObject(document.RootElement, "The tag list");
            RequireKnownFields(root, ListFields, "The tag list");

            var newTables = ReadNames(root, "newTables");
            var tags = ReadTags(root);

            return new UnifiedTagList(newTables, tags);
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The tag list is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static List<UnifiedTagDescription> ReadTags(JsonElement root)
        {
            if (!root.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array || tags.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'tags' must list at least one tag");
            }

            var read = tags.EnumerateArray().Select((tag, index) => ReadTag(tag, index + 1)).ToList();

            var repeated = read.GroupBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? read
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Tag names must be unique, in any case; repeated: {string.Join(", ", repeated)}");
        }

        private static UnifiedTagDescription ReadTag(JsonElement element, int position)
        {
            var tag = RequireObject(element, $"Tag {position}");
            var name = OptionalString(tag, "name");
            var what = string.IsNullOrWhiteSpace(name) ? $"Tag {position}" : $"Tag '{name}'";
            RequireKnownFields(tag, TagFields, what);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs a 'name'");
            }

            // An empty table means the default one, as leaving the field out does.
            var table = string.IsNullOrWhiteSpace(OptionalString(tag, "table")) ? null : OptionalString(tag, "table")!.Trim();
            var link = ReadLink(tag, what);
            var dataType = OptionalString(tag, "dataType");

            if (link != null)
            {
                return dataType == null
                    ? new UnifiedTagDescription(name!.Trim(), table, null, link)
                    : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is linked to a PLC tag and takes its data type from it; leave 'dataType' out");
            }

            return new UnifiedTagDescription(name!.Trim(), table, UnifiedTagDataTypes.Normalise(dataType, what), null);
        }

        private static UnifiedTagLink? ReadLink(JsonElement tag, string what)
        {
            var connection = OptionalString(tag, "connection")?.Trim();
            var plcTag = OptionalString(tag, "plcTag")?.Trim();

            if (string.IsNullOrEmpty(connection) && string.IsNullOrEmpty(plcTag))
            {
                return null;
            }

            return !string.IsNullOrEmpty(connection) && !string.IsNullOrEmpty(plcTag)
                ? new UnifiedTagLink(connection!, plcTag!)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a linked tag needs both 'connection' and 'plcTag'; an internal one needs neither, and a 'dataType'");
        }

        private static List<string> ReadNames(JsonElement root, string field)
        {
            if (!root.TryGetProperty(field, out var names) || names.ValueKind == JsonValueKind.Null)
            {
                return new List<string>();
            }

            if (names.ValueKind != JsonValueKind.Array || names.EnumerateArray().Any(name => name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{field}' must be a list of names");
            }

            return names.EnumerateArray().Select(name => name.GetString()!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void RequireKnownFields(JsonElement owner, HashSet<string> known, string what)
        {
            var unknown = owner.EnumerateObject().Select(property => property.Name).Where(name => !known.Contains(name)).ToList();

            if (unknown.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has fields this tool does not set: {string.Join(", ", unknown)}. It takes: {string.Join(", ", known)}");
            }
        }

        private static JsonElement RequireObject(JsonElement element, string what)
        {
            return element.ValueKind == JsonValueKind.Object
                ? element
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
        }

        private static string? OptionalString(JsonElement owner, string field)
        {
            if (!owner.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{field}' must be a string; got {value.GetRawText()}");
        }
    }
}
