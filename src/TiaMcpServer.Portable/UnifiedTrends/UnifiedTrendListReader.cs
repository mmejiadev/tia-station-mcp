using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedTrends
{
    /// <summary>Reads the trends to show in a WinCC Unified trend control from JSON.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "screen": "Start", "control": "Trend_1",
    ///   "trends": [ { "tag": "Level", "loggingTag": "Level_log", "name": "Nivell", "color": "#FF0000", "mode": "stepped" },
    ///               { "tag": "Pressure" } ],
    ///   "timeRange": { "factor": 10, "base": "minute" } }
    /// </code>
    /// Measured 2026-10-30 on TIA Portal V20: a time range factor outside 1 to 32767 is refused; the modes and
    /// bases are the Openness names, <c>Undefined</c> left out.
    /// </remarks>
    public static class UnifiedTrendListReader
    {
        /// <summary>The highest time range factor TIA Portal takes.</summary>
        public const int MaxFactor = 32767;

        private static readonly string[] ListFields = { "screen", "control", "trends", "timeRange" };

        private static readonly string[] TrendFields = { "tag", "loggingTag", "name", "color", "mode" };

        private static readonly string[] RangeFields = { "factor", "base" };

        private static readonly string[] Modes = { "Points", "Interpolated", "Stepped", "Bar", "Value" };

        private static readonly string[] Bases = { "Millisecond", "Second", "Minute", "Hour", "Day", "Month", "Year" };

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads a trend list.</summary>
        /// <param name="json">The list.</param>
        /// <returns>The control and its trends.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid or a value is not one TIA Portal takes.</exception>
        public static UnifiedTrendList Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = UnifiedTagJson.RequireObject(document.RootElement, "The trend list");
            UnifiedTagJson.RequireKnownFields(root, ListFields, "The trend list");

            var place = (Required(root, "screen", "The trend list", "the screen holding the trend control"), Required(root, "control", "The trend list", "the trend control, drawn with DrawUnifiedScreen"));

            if (!root.TryGetProperty("trends", out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'trends' must be a list of at least one trend");
            }

            var trends = entries.EnumerateArray().Select((entry, index) => ReadTrend(entry, $"Trend {index + 1}")).ToList();

            return new UnifiedTrendList(place, trends, ReadTimeRange(root));
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The trend list is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static UnifiedTrendDescription ReadTrend(JsonElement element, string what)
        {
            var owner = UnifiedTagJson.RequireObject(element, what);
            UnifiedTagJson.RequireKnownFields(owner, TrendFields, what);

            var tag = Required(owner, "tag", what, "the HMI tag it draws");
            var loggingTag = UnifiedTagJson.OptionalString(owner, "loggingTag")?.Trim();
            var color = owner.TryGetProperty("color", out var given) && given.ValueKind != JsonValueKind.Null
                ? (Color?)UnifiedPropertyConverter.Convert(typeof(Color), given, $"{what}: 'color'")
                : null;

            return new UnifiedTrendDescription(
                (tag, string.IsNullOrEmpty(loggingTag) ? null : loggingTag),
                UnifiedTagJson.OptionalString(owner, "name"),
                (color, OneOf(UnifiedTagJson.OptionalString(owner, "mode"), Modes, $"{what}: 'mode'")));
        }

        private static (int Factor, string Base)? ReadTimeRange(JsonElement root)
        {
            if (!root.TryGetProperty("timeRange", out var given) || given.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var range = UnifiedTagJson.RequireObject(given, "'timeRange'");
            UnifiedTagJson.RequireKnownFields(range, RangeFields, "'timeRange'");

            var factor = range.TryGetProperty("factor", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 1 && number <= MaxFactor
                ? number
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'timeRange' needs 'factor', a whole number from 1 to {MaxFactor}");

            return (factor, OneOf(UnifiedTagJson.OptionalString(range, "base"), Bases, "'timeRange': 'base'") ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'timeRange' needs 'base', one of {string.Join(", ", Bases)}"));
        }

        private static string? OneOf(string? given, IReadOnlyList<string> allowed, string what)
        {
            if (given == null)
            {
                return null;
            }

            return allowed.FirstOrDefault(name => string.Equals(name, given.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be one of {string.Join(", ", allowed)}; got \"{given}\"");
        }

        private static string Required(JsonElement owner, string field, string what, string meaning)
        {
            var value = UnifiedTagJson.OptionalString(owner, field);

            return string.IsNullOrWhiteSpace(value)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs '{field}', {meaning}")
                : value!.Trim();
        }
    }
}
