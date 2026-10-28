using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>Reads the WinCC Unified alarm classes and alarms to create from JSON, refusing a list that is incomplete.</summary>
    /// <remarks>
    /// The shape:
    /// <code>
    /// { "classes": [ { "name": "Tolva", "priority": 10 } ],
    ///   "discrete": [ { "name": "Tolva_Plena", "tag": "Alarms", "bit": 3, "trigger": "risingEdge", "class": "Tolva",
    ///                   "priority": 3, "text": "Tolva plena", "info": "Buidar la tolva", "origin": "Tolva", "area": "Linia 1" } ],
    ///   "analog": [ { "name": "Pressio_Alta", "tag": "Pressure", "condition": "upperLimit", "value": 8.5, "class": "Warning",
    ///                 "text": "Pressió alta" } ] }
    /// </code>
    /// An alarm without a class gets <see cref="UnifiedAlarmCommon.DefaultClass"/>, as TIA Portal gives it;
    /// a discrete alarm without a bit takes bit 0 and without a trigger the rising edge. Every alarm needs a
    /// text: one without is accepted by TIA Portal and tells the operator nothing. Whether a tag fits its
    /// alarm, and whether a value fits the tag, is checked once the tags are known. Names are unique among
    /// alarms of one kind, and a tag and bit raise one discrete alarm only (the compile refuses two, as
    /// "The tag/bit combination is also used for other HMI alarms", measured 2026-10-25).
    /// </remarks>
    public static class UnifiedAlarmListReader
    {
        private static readonly string[] ListFields = { "classes", "discrete", "analog" };

        private static readonly string[] CommonFields = { "name", "tag", "class", "priority", "text", "info", "origin", "area" };

        private static readonly string[] DiscreteFields = CommonFields.Concat(new[] { "bit", "trigger" }).ToArray();

        private static readonly string[] AnalogFields = CommonFields.Concat(new[] { "condition", "value" }).ToArray();

        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Reads an alarm list.</summary>
        /// <param name="json">The list.</param>
        /// <returns>The classes and alarms to create.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the JSON is invalid or an entry incomplete.</exception>
        public static UnifiedAlarmList Parse(string json)
        {
            using var document = ParseDocument(json);
            var root = UnifiedTagJson.RequireObject(document.RootElement, "The alarm list");
            UnifiedTagJson.RequireKnownFields(root, ListFields, "The alarm list");

            var list = new UnifiedAlarmList(UnifiedAlarmClassReader.Read(root), ReadEach(root, "discrete", ReadDiscrete), ReadEach(root, "analog", ReadAnalog));
            if (list.Classes.Count + list.Discrete.Count + list.Analog.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The alarm list must hold at least one class or alarm");
            }

            RequireUnique(list.Discrete.Select(alarm => alarm.Common.Name), "Discrete alarm");
            RequireUnique(list.Analog.Select(alarm => alarm.Common.Name), "Analog alarm");
            var shared = list.Discrete.GroupBy(alarm => $"{alarm.Tag} bit {alarm.Bit}", StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).ToList();
            if (shared.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"One tag and bit raises one discrete alarm; shared: {string.Join("; ", shared.Select(group => $"{group.Key} by {string.Join(", ", group.Select(alarm => alarm.Common.Name))}"))}");
            }

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
                throw new PortalException(PortalErrorCode.InvalidParams, $"The alarm list is not valid JSON: {exception.Message}", null, exception);
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
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{field}' must be a list of alarms");
        }

        private static UnifiedDiscreteAlarm ReadDiscrete(JsonElement element, int position)
        {
            var (owner, common, tag, what) = ReadCommon(element, $"Discrete alarm {position}", DiscreteFields);

            return new UnifiedDiscreteAlarm(common, tag, ReadBit(owner, what), UnifiedAlarmJson.OptionalEnum<UnifiedAlarmTrigger>(owner, "trigger", what) ?? UnifiedAlarmTrigger.RisingEdge);
        }

        private static UnifiedAnalogAlarm ReadAnalog(JsonElement element, int position)
        {
            var (owner, common, tag, what) = ReadCommon(element, $"Analog alarm {position}", AnalogFields);
            var condition = UnifiedAlarmJson.OptionalEnum<UnifiedAlarmCondition>(owner, "condition", what)
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs a 'condition': {string.Join(", ", Enum.GetNames(typeof(UnifiedAlarmCondition)).Select(UnifiedAlarmJson.Camel))}");

            return owner.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number
                ? new UnifiedAnalogAlarm(common, tag, condition, value.GetRawText())
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs a number 'value' to compare its tag with");
        }

        private static (JsonElement Owner, UnifiedAlarmCommon Common, string Tag, string What) ReadCommon(JsonElement element, string fallback, IReadOnlyCollection<string> fields)
        {
            var owner = UnifiedTagJson.RequireObject(element, fallback);
            var name = UnifiedAlarmJson.RequireName(owner, fallback);
            var what = $"{fallback.Split(' ')[0]} alarm '{name}'";
            UnifiedTagJson.RequireKnownFields(owner, fields, what);

            var tag = UnifiedTagJson.OptionalString(owner, "tag");
            var text = UnifiedTagJson.OptionalString(owner, "text");
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(text))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs a 'tag' that raises it and a 'text' the operator reads");
            }

            var alarmClass = UnifiedTagJson.OptionalString(owner, "class");
            var texts = new UnifiedAlarmTexts(text!, UnifiedTagJson.OptionalString(owner, "info"), UnifiedTagJson.OptionalString(owner, "origin"), UnifiedTagJson.OptionalString(owner, "area"));
            var common = new UnifiedAlarmCommon(name, string.IsNullOrWhiteSpace(alarmClass) ? UnifiedAlarmCommon.DefaultClass : alarmClass!.Trim(), UnifiedAlarmJson.OptionalPriority(owner, what), texts);

            return (owner, common, tag!.Trim(), what);
        }

        private static uint ReadBit(JsonElement owner, string what)
        {
            if (!owner.TryGetProperty("bit", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return 0;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var bit)
                ? bit
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'bit' must be a whole number from 0; got {value.GetRawText()}");
        }

        private static void RequireUnique(IEnumerable<string> keys, string what)
        {
            var repeated = keys.GroupBy(key => key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            if (repeated.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} names must be unique, in any case; repeated: {string.Join(", ", repeated)}");
            }
        }
    }
}
