using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.HmiTags;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Siemens
{
    /// <summary>Adds a WinCC Unified tag's settings to its description, in the shape CreateUnifiedTags takes them.</summary>
    /// <remarks>
    /// Only what differs from TIA Portal's defaults is written, so a plain tag reads as before; a WString
    /// always gives its length, as part of its type. A number tag's values come back as JSON numbers, a
    /// text tag's as text, and a limit held by a tag as the tag's name, as they are written. A linked
    /// tag's acquisition is written unless it is TIA Portal's default, cyclic on use every second.
    /// <c>tagType</c> and thresholds are read only: Openness V20 cannot create thresholds.
    /// Through the typed properties only, never <c>GetAttributeInfos()</c>.
    /// </remarks>
    internal sealed class UnifiedTagSettingsDescriber
    {
        private const string DefaultCycle = "T1s";

        private readonly Language _language;

        /// <summary>Creates a describer.</summary>
        /// <param name="language">The project's editing language, the one a comment is read in.</param>
        public UnifiedTagSettingsDescriber(Language language)
        {
            _language = language;
        }

        /// <summary>Adds the settings of one tag.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="described">Its description so far, which gets the settings.</param>
        /// <param name="isLinked">Whether the tag is linked to a PLC tag.</param>
        public void Describe(HmiTag tag, JsonObject described, bool isLinked)
        {
            DescribeOptions(tag, described);
            DescribeLimits(tag, described);

            if (isLinked)
            {
                DescribePlcReading(tag, described);
            }

            var thresholds = tag.Thresholds.Select(threshold => (JsonNode)new JsonObject
            {
                ["name"] = threshold.Name,
                ["mode"] = Camel(threshold.Mode.ToString()),
                ["value"] = Value(threshold.ValueType, threshold.Value)
            }).ToArray();
            if (thresholds.Length > 0)
            {
                described["thresholds"] = new JsonArray(thresholds);
            }
        }

        private void DescribeOptions(HmiTag tag, JsonObject described)
        {
            if (tag.TagType != HmiTagType.Simple)
            {
                described["tagType"] = tag.TagType.ToString().ToLowerInvariant();
            }

            var comment = tag.Comment.Items.Find(_language)?.Text;
            if (!string.IsNullOrEmpty(comment))
            {
                described["comment"] = comment;
            }

            var initial = tag.InitialValue?.ToString();
            if (!string.IsNullOrEmpty(initial))
            {
                described["initialValue"] = IsNumber(tag.DataType) ? Number(initial!) : JsonValue.Create(initial);
            }

            if (tag.Persistent)
            {
                described["persistent"] = true;
            }

            if (tag.DataType == "WString")
            {
                described["maxLength"] = tag.MaxLength;
            }
        }

        private static void DescribeLimits(HmiTag tag, JsonObject described)
        {
            var limits = new JsonObject();
            AddLimit(limits, "lower", tag.InitialMinValue);
            AddLimit(limits, "upper", tag.InitialMaxValue);

            if (limits.Count > 0)
            {
                described["limits"] = limits;
            }
        }

        private static void AddLimit(JsonObject limits, string side, Range range)
        {
            if (range.ValueType != HmiLimitValueType.None)
            {
                limits[side] = Value(range.ValueType, range.Value);
            }
        }

        private static void DescribePlcReading(HmiTag tag, JsonObject described)
        {
            if (tag.AcquisitionMode != HmiAcquisitionMode.CyclicOnUse || tag.AcquisitionCycle != DefaultCycle)
            {
                var acquisition = new JsonObject { ["mode"] = Camel(tag.AcquisitionMode.ToString()) };
                if (!string.IsNullOrEmpty(tag.AcquisitionCycle))
                {
                    acquisition["cycle"] = tag.AcquisitionCycle;
                }

                described["acquisition"] = acquisition;
            }

            if (tag.LinearScaling)
            {
                described["scaling"] = new JsonObject
                {
                    ["plc"] = new JsonArray(Number(tag.PlcStartValue), Number(tag.PlcEndValue)),
                    ["hmi"] = new JsonArray(Number(tag.HmiStartValue), Number(tag.HmiEndValue))
                };
            }

            if (tag.SubstituteValue.SubstituteValueUsage != HmiSubstituteValueUsage.None)
            {
                described["substitute"] = new JsonObject
                {
                    ["usage"] = Camel(tag.SubstituteValue.SubstituteValueUsage.ToString()),
                    ["value"] = IsNumber(tag.DataType) ? Number(tag.SubstituteValue.Value) : JsonValue.Create(tag.SubstituteValue.Value?.ToString())
                };
            }
        }

        private static JsonValue? Value(HmiLimitValueType kind, object? value)
        {
            return kind == HmiLimitValueType.Tag ? JsonValue.Create(value?.ToString()) : Number(value?.ToString() ?? string.Empty);
        }

        /// <summary>Whether the tag's values are numbers: the numeric types, and a Bool's 0 and 1.</summary>
        private static bool IsNumber(string dataType)
        {
            return UnifiedTagValues.IsNumeric(dataType) || dataType == "Bool";
        }

        /// <summary>
        /// A number tag's value as a JSON number: exact as a decimal when it fits one (a ULInt does), as a
        /// double beyond (an LReal of 1E+30), and as text only when TIA Portal holds something that is not
        /// a number at all.
        /// </summary>
        private static JsonValue? Number(object? value)
        {
            var text = System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
            {
                return JsonValue.Create(exact);
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var large)
                ? JsonValue.Create(large)
                : JsonValue.Create(text);
        }

        private static string Camel(string name)
        {
            return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
