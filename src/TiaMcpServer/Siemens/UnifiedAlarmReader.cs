using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiAlarm.HmiAlarmCommon;
using System.Drawing;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedAlarms;
using TiaMcpServer.UnifiedScreens;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a WinCC Unified HMI's alarm classes and alarms, in the shape CreateUnifiedAlarms takes them.</summary>
    /// <remarks>
    /// Read only, and refused by CreateUnifiedAlarms: an alarm's <c>id</c>, which TIA Portal gives, and a
    /// class's <c>system</c>, true for the 18 classes every HMI has. A tag of <c>&lt;No tag&gt;</c> is left
    /// out, as is an empty info text, origin or area.
    /// </remarks>
    internal static class UnifiedAlarmReader
    {
        private const string NoTag = "<No tag>";

        /// <summary>Reads every alarm class and alarm.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="language">The project's editing language, the one texts are read in.</param>
        /// <returns><c>{ classes: [...], discrete: [...], analog: [...] }</c>.</returns>
        public static JsonObject Read(UnifiedHmiSoftware hmi, Language language)
        {
            return new JsonObject
            {
                ["classes"] = new JsonArray(hmi.AlarmClasses.Select(found => (JsonNode)DescribeClass(found)).ToArray()),
                ["discrete"] = new JsonArray(hmi.DiscreteAlarms.Select(alarm => (JsonNode)DescribeDiscrete(alarm, language)).ToArray()),
                ["analog"] = new JsonArray(hmi.AnalogAlarms.Select(alarm => (JsonNode)DescribeAnalog(alarm, language)).ToArray())
            };
        }

        private static JsonObject DescribeClass(HmiAlarmClass found)
        {
            return new JsonObject
            {
                ["name"] = found.Name,
                ["system"] = found.IsSystem,
                ["priority"] = (int)found.Priority,
                ["stateMachine"] = UnifiedAlarmJson.Camel(found.StateMachine.ToString()),
                ["states"] = new JsonObject
                {
                    ["raised"] = DescribeVisuals(found.RaisedState),
                    ["acknowledged"] = DescribeVisuals(found.AcknowledgedState),
                    ["cleared"] = DescribeVisuals(found.ClearedState),
                    ["acknowledgedCleared"] = DescribeVisuals(found.AcknowledgedClearedState)
                }
            };
        }

        private static JsonObject DescribeVisuals(AlarmStatusVisuals visuals)
        {
            return new JsonObject
            {
                ["backColor"] = Channels(visuals.BackColor),
                ["textColor"] = Channels(visuals.TextColor),
                ["flashing"] = visuals.Flashing
            };
        }

        private static JsonObject DescribeDiscrete(HmiDiscreteAlarm alarm, Language language)
        {
            var described = new JsonObject { ["name"] = alarm.Name, ["id"] = alarm.Id };
            AddTag(described, alarm.RaisedStateTag);
            described["bit"] = alarm.RaisedStateTagBitNumber;
            described["trigger"] = alarm.TriggerMode switch
            {
                HmiDiscreteAlarmTriggerMode.OnRisingEdge => "risingEdge",
                HmiDiscreteAlarmTriggerMode.OnFallingEdge => "fallingEdge",
                // A mode this tool does not know is shown as TIA Portal names it, never as one it is not.
                _ => UnifiedAlarmJson.Camel(alarm.TriggerMode.ToString())
            };
            AddCommon(described, alarm, language);

            return described;
        }

        private static JsonObject DescribeAnalog(HmiAnalogAlarm alarm, Language language)
        {
            var described = new JsonObject { ["name"] = alarm.Name, ["id"] = alarm.Id };
            AddTag(described, alarm.RaisedStateTag);
            described["condition"] = UnifiedAlarmJson.Camel(alarm.Condition.ToString());
            described["value"] = JsonValue.Create(System.Convert.ToDouble(alarm.ConditionValue, System.Globalization.CultureInfo.InvariantCulture));
            AddCommon(described, alarm, language);

            return described;
        }

        private static void AddTag(JsonObject described, string tag)
        {
            if (!string.IsNullOrEmpty(tag) && tag != NoTag)
            {
                described["tag"] = tag;
            }
        }

        private static void AddCommon(JsonObject described, AlarmBase alarm, Language language)
        {
            described["class"] = alarm.AlarmClass;
            described["priority"] = (int)alarm.Priority;
            described["text"] = Text(alarm.EventText, language) ?? string.Empty;
            AddIfSet(described, "info", Text(alarm.InfoText, language));
            AddIfSet(described, "origin", alarm.Origin);
            AddIfSet(described, "area", alarm.Area);
        }

        private static void AddIfSet(JsonObject described, string field, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                described[field] = value;
            }
        }

        private static string? Text(MultilingualText text, Language language)
        {
            var stored = text.Items.Find(language)?.Text;

            return stored == null || stored == UnifiedRichText.Empty ? null : UnifiedRichText.Read(stored);
        }

        /// <remarks>A colour that is not opaque keeps its alpha as a fourth channel, which CreateUnifiedAlarms takes back.</remarks>
        private static string Channels(Color colour)
        {
            return colour.A == byte.MaxValue ? $"{colour.R}, {colour.G}, {colour.B}" : $"{colour.R}, {colour.G}, {colour.B}, {colour.A}";
        }
    }
}
