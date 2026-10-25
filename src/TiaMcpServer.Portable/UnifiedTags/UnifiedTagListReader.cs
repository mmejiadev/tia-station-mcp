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
    /// { "newTables": [ "Motors", "Plant/Area_1/Valves" ],
    ///   "tags": [
    ///     { "name": "Speed_SP", "table": "Motors", "dataType": "Int", "limits": { "lower": 0, "upper": 1500 } },
    ///     { "name": "Levels", "dataType": "Array [0..3] of Real" },
    ///     { "name": "Motor_Run", "connection": "HMI_Connection_1", "plcTag": "Motor_Run" } ] }
    /// </code>
    /// A tag is internal, with a <c>dataType</c>, or linked, with a <c>connection</c> and a
    /// <c>plcTag</c>; never both, because a linked tag takes its type from the PLC tag (measured
    /// 2026-10-22: TIA Portal fills it in). Its settings are read by <see cref="UnifiedTagSettingsReader"/>.
    /// A new table is a name or a path of groups ending in the table's name; the groups are created when
    /// missing. Table names are unique on the whole HMI, groups or not (measured 2026-10-25), so a table is
    /// found by its name alone. A field nobody reads is refused rather than ignored.
    /// </remarks>
    public static class UnifiedTagListReader
    {
        private const char GroupSeparator = '/';

        private static readonly HashSet<string> TagFields = new HashSet<string>(new[] { "name", "table", "dataType", "connection", "plcTag" }.Concat(UnifiedTagSettingsReader.Fields), StringComparer.Ordinal);

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
            var root = UnifiedTagJson.RequireObject(document.RootElement, "The tag list");
            UnifiedTagJson.RequireKnownFields(root, ListFields, "The tag list");

            var newTables = ReadTablePaths(root);
            var tags = ReadTags(root);

            return new UnifiedTagList(newTables, tags);
        }

        /// <summary>The name of the table a path ends in.</summary>
        /// <param name="path">A table's name, or its path of groups, e.g. <c>Plant/Area_1/Valves</c>.</param>
        /// <returns>The table's name, e.g. <c>Valves</c>.</returns>
        public static string TableName(string path)
        {
            return (path ?? throw new ArgumentNullException(nameof(path))).Split(GroupSeparator).Last();
        }

        /// <summary>The groups a table path goes through, outermost first.</summary>
        /// <param name="path">A table's path, e.g. <c>Plant/Area_1/Valves</c>.</param>
        /// <returns>The group names, e.g. <c>Plant</c> and <c>Area_1</c>; empty for a table outside any group.</returns>
        public static IReadOnlyList<string> GroupsOf(string path)
        {
            var parts = (path ?? throw new ArgumentNullException(nameof(path))).Split(GroupSeparator);

            return parts.Take(parts.Length - 1).ToList();
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
            var tag = UnifiedTagJson.RequireObject(element, $"Tag {position}");
            var name = UnifiedTagJson.OptionalString(tag, "name");
            var what = string.IsNullOrWhiteSpace(name) ? $"Tag {position}" : $"Tag '{name}'";
            UnifiedTagJson.RequireKnownFields(tag, TagFields, what);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs a 'name'");
            }

            // An empty table means the default one, as leaving the field out does.
            var table = string.IsNullOrWhiteSpace(UnifiedTagJson.OptionalString(tag, "table")) ? null : UnifiedTagJson.OptionalString(tag, "table")!.Trim();
            var source = ReadSource(tag, what);

            return new UnifiedTagDescription(name!.Trim(), table, source, UnifiedTagSettingsReader.Read(tag, source, what));
        }

        private static UnifiedTagSource ReadSource(JsonElement tag, string what)
        {
            var link = ReadLink(tag, what);
            var dataType = UnifiedTagJson.OptionalString(tag, "dataType");

            if (link != null)
            {
                return dataType == null
                    ? UnifiedTagSource.Linked(link)
                    : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is linked to a PLC tag and takes its data type from it; leave 'dataType' out");
            }

            return UnifiedTagSource.Internal(UnifiedTagDataTypes.NormaliseInternal(dataType, what));
        }

        private static UnifiedTagLink? ReadLink(JsonElement tag, string what)
        {
            var connection = UnifiedTagJson.OptionalString(tag, "connection")?.Trim();
            var plcTag = UnifiedTagJson.OptionalString(tag, "plcTag")?.Trim();

            if (string.IsNullOrEmpty(connection) && string.IsNullOrEmpty(plcTag))
            {
                return null;
            }

            return !string.IsNullOrEmpty(connection) && !string.IsNullOrEmpty(plcTag)
                ? new UnifiedTagLink(connection!, plcTag!)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a linked tag needs both 'connection' and 'plcTag'; an internal one needs neither, and a 'dataType'");
        }

        private static List<string> ReadTablePaths(JsonElement root)
        {
            if (!root.TryGetProperty("newTables", out var names) || names.ValueKind == JsonValueKind.Null)
            {
                return new List<string>();
            }

            if (names.ValueKind != JsonValueKind.Array || names.EnumerateArray().Any(name => name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'newTables' must be a list of names");
            }

            var paths = names.EnumerateArray().Select(name => NormalisePath(name.GetString()!)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var repeated = paths.GroupBy(TableName, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? paths
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Table names are unique on the whole HMI, groups or not; repeated: {string.Join(", ", repeated)}");
        }

        private static string NormalisePath(string path)
        {
            var parts = path.Split(GroupSeparator).Select(part => part.Trim()).ToList();

            return parts.Any(part => part.Length == 0)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"'{path}' has an empty group or table name; write it as 'Group/Table'")
                : string.Join(GroupSeparator.ToString(), parts);
        }
    }
}
