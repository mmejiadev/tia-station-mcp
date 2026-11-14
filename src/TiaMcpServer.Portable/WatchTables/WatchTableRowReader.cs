using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.WatchTables
{
    /// <summary>
    /// Reads the rows to write from JSON: <c>{"rows": [{"address": "%Q0.0", "format": "Bool", "value": "TRUE", "comment": "Lamp"}]}</c>.
    /// </summary>
    /// <remarks>
    /// All or nothing: every row is checked with <see cref="WatchTableRowRules"/> and every problem is named at once,
    /// as an import replaces all of a table's rows and a half-checked list would replace them with less than asked.
    /// </remarks>
    public static class WatchTableRowReader
    {
        private const int MaximumRows = 1000;

        /// <summary>Reads and checks the rows.</summary>
        /// <param name="json">The rows as JSON.</param>
        /// <param name="kind">The table they go in.</param>
        /// <returns>The rows, checked, values normalized.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming every problem.</exception>
        public static IReadOnlyList<WatchTableRow> Parse(string json, WatchTableKind kind)
        {
            var rows = ReadRows(json);
            var problems = rows.SelectMany((row, index) => WatchTableRowRules.Check(kind, row).Select(problem => $"row {index + 1}: {problem}")).ToList();

            if (problems.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Nothing was written. " + string.Join("; ", problems));
            }

            return rows
                .Select(row => new WatchTableRow(row.Address, row.DisplayFormat, WatchTableRowRules.NormalizeValue(row.DisplayFormat, row.Value), row.Comment))
                .ToList();
        }

        private static List<WatchTableRow> ReadRows(string json)
        {
            JsonElement rows;

            try
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);

                if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("rows", out rows) || rows.ValueKind != JsonValueKind.Array)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "The rows are written as {\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}]}");
                }

                return ReadEach(rows);
            }
            catch (JsonException ex)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The rows are not valid JSON: {ex.Message}", null, ex);
            }
        }

        private static List<WatchTableRow> ReadEach(JsonElement rows)
        {
            var count = rows.GetArrayLength();

            if (count == 0 || count > MaximumRows)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Give between 1 and {MaximumRows} rows; there are {count}");
            }

            return rows.EnumerateArray().Select(ReadRow).ToList();
        }

        private static WatchTableRow ReadRow(JsonElement row)
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Each row is an object: {\"address\": \"%Q0.0\", \"format\": \"Bool\"}");
            }

            return new WatchTableRow(Text(row, "address"), Text(row, "format"), Text(row, "value"), Text(row, "comment"));
        }

        private static string Text(JsonElement row, string name)
        {
            if (!row.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                return string.Empty;
            }

            return (property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.GetRawText()).Trim();
        }
    }
}
