using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedAlarms;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>Reads the log settings to change from JSON.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "logs": [ { "name": "Log_Tolva", "maxSize": 500, "duration": { "days": 30 },
    ///               "segment": { "maxSize": 50, "duration": { "hours": 12 }, "start": "2026-01-01T00:00:00" },
    ///               "backupPath": "D:\\Backup" } ] }
    /// </code>
    /// Only what is given changes. A period given is given whole: a part left out is 0, so <c>{ "days": 30 }</c>
    /// is 30 days and not 30 days plus what the hours were. <c>"backupPath": ""</c> turns the backup off.
    /// </remarks>
    public static class UnifiedLogSettingsListReader
    {
        private const string StartFormat = "yyyy-MM-ddTHH:mm:ss";

        private static readonly string[] ListFields = { "logs" };

        private static readonly string[] LogFields = { "name", "maxSize", "duration", "segment", "backupPath" };

        private static readonly string[] SegmentFields = { "maxSize", "duration", "start" };

        private static readonly string[] DurationFields = { "days", "hours", "minutes", "seconds" };

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads a list of log settings.</summary>
        /// <param name="json">The list.</param>
        /// <returns>One request per log.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid, a log is named twice or a value is out of range.</exception>
        public static IReadOnlyList<UnifiedLogSettingsRequest> Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = UnifiedTagJson.RequireObject(document.RootElement, "The log settings");
            UnifiedTagJson.RequireKnownFields(root, ListFields, "The log settings");

            if (!root.TryGetProperty("logs", out var logs) || logs.ValueKind != JsonValueKind.Array || logs.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "'logs' must be a list of at least one log");
            }

            var requests = logs.EnumerateArray().Select((entry, index) => ReadLog(entry, index + 1)).ToList();
            var repeated = requests.GroupBy(request => request.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? requests
                : throw new PortalException(PortalErrorCode.InvalidParams, $"Log named twice: '{string.Join("', '", repeated)}'");
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The log settings are not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static UnifiedLogSettingsRequest ReadLog(JsonElement element, int position)
        {
            var owner = UnifiedTagJson.RequireObject(element, $"Log {position}");
            var name = UnifiedAlarmJson.RequireName(owner, $"Log {position}");
            var what = $"Log '{name}'";
            UnifiedTagJson.RequireKnownFields(owner, LogFields, what);

            var segment = owner.TryGetProperty("segment", out var given) && given.ValueKind != JsonValueKind.Null
                ? ReadSegment(UnifiedTagJson.RequireObject(given, $"{what}: 'segment'"), $"{what}: segment")
                : (null, null, null);

            var request = new UnifiedLogSettingsRequest(name, (OptionalSize(owner, what), OptionalDuration(owner, what)), segment, ReadBackupPath(owner, what));

            return request.IsEmpty
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} names no setting to change; give maxSize, duration, segment or backupPath")
                : request;
        }

        /// <remarks>Only <c>""</c> turns the backup off: a path of spaces is more likely a value left unfilled than a wish to stop backing up.</remarks>
        private static string? ReadBackupPath(JsonElement owner, string what)
        {
            var path = UnifiedTagJson.OptionalString(owner, "backupPath");

            return path != null && path.Length > 0 && path.Trim().Length == 0
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'backupPath' is only spaces; give a path, or \"\" to turn the backup off")
                : path?.Trim();
        }

        /// <remarks>
        /// The start is read in one shape only, <c>yyyy-MM-ddTHH:mm:ss</c>, with no time zone: TIA Portal holds a
        /// local time, and a <c>Z</c> or an offset would be moved to this machine's time on the way.
        /// </remarks>
        private static (uint? MaxSize, UnifiedLogDuration? Duration, DateTime? Start) ReadSegment(JsonElement segment, string what)
        {
            UnifiedTagJson.RequireKnownFields(segment, SegmentFields, what);
            var start = UnifiedTagJson.OptionalString(segment, "start");
            DateTime parsedStart = default;

            if (start != null && !DateTime.TryParseExact(start, StartFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedStart))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'start' must be a local date and time written {StartFormat}, e.g. \"2026-01-01T00:00:00\"; got \"{start}\"");
            }

            return (OptionalSize(segment, what), OptionalDuration(segment, what), start == null ? (DateTime?)null : parsedStart);
        }

        private static uint? OptionalSize(JsonElement owner, string what)
        {
            if (!owner.TryGetProperty("maxSize", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var size) && size > 0
                ? size
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'maxSize' must be a whole number from 1; got {value.GetRawText()}");
        }

        private static UnifiedLogDuration? OptionalDuration(JsonElement owner, string what)
        {
            if (!owner.TryGetProperty("duration", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var period = UnifiedTagJson.RequireObject(value, $"{what}: 'duration'");
            UnifiedTagJson.RequireKnownFields(period, DurationFields, $"{what}: 'duration'");

            return new UnifiedLogDuration(
                Part(period, "days", UnifiedLogDuration.MaxDays, what),
                Part(period, "hours", UnifiedLogDuration.MaxHours, what),
                Part(period, "minutes", UnifiedLogDuration.MaxMinutesOrSeconds, what),
                Part(period, "seconds", UnifiedLogDuration.MaxMinutesOrSeconds, what));
        }

        /// <remarks>TIA Portal refuses hours above 23 and minutes or seconds above 59 (measured 2026-10-30); for the days see <see cref="UnifiedLogDuration.MaxDays"/>.</remarks>
        private static uint Part(JsonElement period, string field, uint max, string what)
        {
            if (!period.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return 0;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var part) && part <= max
                ? part
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' must be a whole number from 0 to {max}; got {value.GetRawText()}");
        }
    }
}
