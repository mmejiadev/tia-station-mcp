using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedAlarms;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>Reads the WinCC Unified data logs, alarm logs and logging tags to create from JSON.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "dataLogs": [ { "name": "Log_Tolva" } ],
    ///   "alarmLogs": [ { "name": "Alarmes", "classes": [ "Alarm", "Warning" ] } ],
    ///   "loggingTags": [ { "tag": "Nivell", "name": "Nivell_log", "dataLog": "Log_Tolva", "mode": "cyclic", "cycle": "T1s", "cycleFactor": 5 } ] }
    /// </code>
    /// What TIA Portal V20 takes and only the compile refuses is refused here (measured 2026-10-28): a
    /// logging tag with no data log ("does not have a valid configuration in the log", and TIA Portal
    /// leaves it empty as soon as a second data log exists), a cyclic logging tag with no cycle ("The
    /// value is invalid"), and an alarm log with no class ("No alarm class is configured for the alarm
    /// log"). A cycle on a logging tag that is not cyclic is refused too, rather than kept unused. A
    /// logging tag's name is unique among the logging tags of its HMI tag, not on the whole HMI.
    /// </remarks>
    public static class UnifiedLoggingListReader
    {
        private static readonly string[] ListFields = { "dataLogs", "alarmLogs", "loggingTags" };

        private static readonly string[] DataLogFields = { "name" };

        private static readonly string[] AlarmLogFields = { "name", "classes" };

        private static readonly string[] LoggingTagFields = { "tag", "name", "dataLog", "mode", "cycle", "cycleFactor" };

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads a logging list.</summary>
        /// <param name="json">The list.</param>
        /// <returns>The logs and logging tags to create.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid or an entry incomplete.</exception>
        public static UnifiedLoggingList Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = UnifiedTagJson.RequireObject(document.RootElement, "The logging list");
            UnifiedTagJson.RequireKnownFields(root, ListFields, "The logging list");

            var list = new UnifiedLoggingList(
                ReadEach(root, "dataLogs", ReadDataLog),
                ReadEach(root, "alarmLogs", ReadAlarmLog),
                ReadEach(root, "loggingTags", ReadLoggingTag));

            if (list.DataLogs.Count + list.AlarmLogs.Count + list.LoggingTags.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The logging list must hold at least one data log, alarm log or logging tag");
            }

            RequireUnique(list.DataLogs, "Data log");
            RequireUnique(list.AlarmLogs.Select(log => log.Name), "Alarm log");
            RequireNoSharedLogName(list);
            RequireUnique(list.LoggingTags.Select(logged => $"{logged.Name}' of tag '{logged.Tag}"), "Logging tag");
            RequireUnique(list.AlarmLogs.SelectMany(log => log.Classes), "Alarm class logged to more than one alarm log; class");

            return list;
        }

        private static JsonDocument ParseDocument(string json)
        {
            try
            {
                return JsonDocument.Parse(json ?? string.Empty, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The logging list is not valid JSON: {exception.Message}", null, exception);
            }
        }

        private static IReadOnlyList<T> ReadEach<T>(JsonElement root, string field, Func<JsonElement, int, T> read)
        {
            if (!root.TryGetProperty(field, out var entries) || entries.ValueKind == JsonValueKind.Null)
            {
                return Array.Empty<T>();
            }

            return entries.ValueKind == JsonValueKind.Array
                ? entries.EnumerateArray().Select((entry, index) => read(entry, index + 1)).ToList()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{field}' must be a list");
        }

        private static string ReadDataLog(JsonElement element, int position)
        {
            var owner = UnifiedTagJson.RequireObject(element, $"Data log {position}");
            var name = UnifiedAlarmJson.RequireName(owner, $"Data log {position}");
            UnifiedTagJson.RequireKnownFields(owner, DataLogFields, $"Data log '{name}'");

            return name;
        }

        private static UnifiedAlarmLogDescription ReadAlarmLog(JsonElement element, int position)
        {
            var owner = UnifiedTagJson.RequireObject(element, $"Alarm log {position}");
            var name = UnifiedAlarmJson.RequireName(owner, $"Alarm log {position}");
            var what = $"Alarm log '{name}'";
            UnifiedTagJson.RequireKnownFields(owner, AlarmLogFields, what);

            if (!owner.TryGetProperty("classes", out var classes) || classes.ValueKind != JsonValueKind.Array || classes.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs 'classes', a list of at least one alarm class: the compile refuses an alarm log no class is logged to");
            }

            var names = classes.EnumerateArray().Select(entry => ReadClassName(entry, what)).ToList();
            RequireUnique(names, $"{what}: alarm class");

            return new UnifiedAlarmLogDescription(name, names);
        }

        private static string ReadClassName(JsonElement entry, string what)
        {
            var name = entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;

            return string.IsNullOrWhiteSpace(name)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: each class is a name, e.g. \"Alarm\"; got {entry.GetRawText()}")
                : name!.Trim();
        }

        private static UnifiedLoggingTag ReadLoggingTag(JsonElement element, int position)
        {
            var owner = UnifiedTagJson.RequireObject(element, $"Logging tag {position}");
            var name = UnifiedAlarmJson.RequireName(owner, $"Logging tag {position}");
            var what = $"Logging tag '{name}'";
            UnifiedTagJson.RequireKnownFields(owner, LoggingTagFields, what);

            var tag = RequireText(owner, "tag", what, "the HMI tag to log");
            var dataLog = RequireText(owner, "dataLog", what, "the data log to write to; the compile refuses a logging tag without one");

            return new UnifiedLoggingTag(tag, name, dataLog, ReadTiming(owner, what));
        }

        private static (UnifiedLoggingMode Mode, string? Cycle, uint? CycleFactor) ReadTiming(JsonElement owner, string what)
        {
            var mode = UnifiedAlarmJson.OptionalEnum<UnifiedLoggingMode>(owner, "mode", what) ?? UnifiedLoggingMode.OnChange;
            var cycle = UnifiedTagJson.OptionalString(owner, "cycle")?.Trim();
            var factor = OptionalCycleFactor(owner, what);

            if (mode == UnifiedLoggingMode.Cyclic && string.IsNullOrEmpty(cycle))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is cyclic and needs a 'cycle', e.g. \"T1s\": the compile refuses a cyclic logging tag without one");
            }

            if (mode != UnifiedLoggingMode.Cyclic && (!string.IsNullOrEmpty(cycle) || factor != null))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has a 'cycle' or 'cycleFactor' but logs {UnifiedAlarmJson.Camel(mode.ToString())}; they are only for \"mode\": \"cyclic\"");
            }

            return (mode, string.IsNullOrEmpty(cycle) ? null : cycle, factor);
        }

        private static uint? OptionalCycleFactor(JsonElement owner, string what)
        {
            if (!owner.TryGetProperty("cycleFactor", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var factor) && factor > 0
                ? factor
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'cycleFactor' must be a whole number from 1, the cycles between two writes; got {value.GetRawText()}");
        }

        /// <remarks>Measured 2026-10-28 on TIA Portal V20: data logs and alarm logs share one set of names, in any case.</remarks>
        private static void RequireNoSharedLogName(UnifiedLoggingList list)
        {
            var shared = list.DataLogs.Intersect(list.AlarmLogs.Select(log => log.Name), StringComparer.OrdinalIgnoreCase).ToList();

            if (shared.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{string.Join("', '", shared)}' is asked for as a data log and as an alarm log; the two share one set of names");
            }
        }

        private static string RequireText(JsonElement owner, string field, string what, string meaning)
        {
            var value = UnifiedTagJson.OptionalString(owner, field);

            return string.IsNullOrWhiteSpace(value)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs '{field}', {meaning}")
                : value!.Trim();
        }

        private static void RequireUnique(IEnumerable<string> names, string what)
        {
            var repeated = names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} named twice: '{string.Join("', '", repeated)}'");
            }
        }
    }
}
