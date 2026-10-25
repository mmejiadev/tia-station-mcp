using System;
using System.Collections.Generic;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>Reads a tag's settings from its JSON object, refusing what its kind of tag does not take.</summary>
    /// <remarks>
    /// The shape, every field optional:
    /// <code>
    /// { "comment": "Tank level", "initialValue": 0, "persistent": true, "maxLength": 20,
    ///   "limits": { "lower": 0, "upper": "Level_Max" },
    ///   "acquisition": { "mode": "cyclicOnUse", "cycle": "T500ms" },
    ///   "scaling": { "plc": [0, 27648], "hmi": [0, 100] },
    ///   "substitute": { "usage": "invalidValue", "value": 0 } }
    /// </code>
    /// A number limit is a constant, a string one names the HMI tag that holds it. What a kind of tag does
    /// not take is refused here, not left for TIA Portal to refuse as a disabled field: retention and a
    /// WString length are an internal tag's; acquisition, scaling and the substitute value a linked one's.
    /// An internal tag's values are checked against its type now; a linked tag's once TIA Portal has filled
    /// its type in.
    /// </remarks>
    internal static class UnifiedTagSettingsReader
    {
        /// <summary>The fields a tag's object takes for its settings.</summary>
        public static readonly string[] Fields = { "comment", "initialValue", "persistent", "maxLength", "limits", "acquisition", "scaling", "substitute" };

        private static readonly string[] LimitFields = { "lower", "upper" };

        private static readonly string[] AcquisitionFields = { "mode", "cycle" };

        private static readonly string[] ScalingFields = { "plc", "hmi" };

        private static readonly string[] SubstituteFields = { "usage", "value" };

        private static readonly Dictionary<string, UnifiedAcquisitionMode> Modes = new Dictionary<string, UnifiedAcquisitionMode>(StringComparer.OrdinalIgnoreCase)
        {
            ["cyclicOnUse"] = UnifiedAcquisitionMode.CyclicOnUse,
            ["onDemand"] = UnifiedAcquisitionMode.OnDemand
        };

        private static readonly Dictionary<string, UnifiedSubstituteUsage> Usages = new Dictionary<string, UnifiedSubstituteUsage>(StringComparer.OrdinalIgnoreCase)
        {
            ["invalidValue"] = UnifiedSubstituteUsage.InvalidValue,
            ["rangeViolation"] = UnifiedSubstituteUsage.RangeViolation,
            ["invalidValueOrRangeViolation"] = UnifiedSubstituteUsage.InvalidValueOrRangeViolation
        };

        /// <summary>Reads the settings of one tag.</summary>
        /// <param name="tag">The tag's JSON object.</param>
        /// <param name="source">Its data type, or the PLC tag it is linked to.</param>
        /// <param name="what">The tag, as a refusal names it.</param>
        /// <returns>The settings; <see cref="UnifiedTagSettings.None"/> when it has none.</returns>
        public static UnifiedTagSettings Read(JsonElement tag, UnifiedTagSource source, string what)
        {
            return new UnifiedTagSettings(ReadOptions(tag, source, what), ReadLimits(tag, source, what), ReadPlcReading(tag, source, what));
        }

        private static UnifiedTagOptions ReadOptions(JsonElement tag, UnifiedTagSource source, string what)
        {
            var persistent = UnifiedTagJson.OptionalBool(tag, "persistent");
            if (persistent != null && source.IsLinked)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: only an internal tag is retained; on a linked tag 'persistent' is a disabled field");
            }

            return new UnifiedTagOptions(UnifiedTagJson.OptionalString(tag, "comment"), ReadInitialValue(tag, source, what), persistent, ReadMaxLength(tag, source, what));
        }

        private static string? ReadInitialValue(JsonElement tag, UnifiedTagSource source, string what)
        {
            if (!tag.TryGetProperty("initialValue", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var text = UnifiedTagJson.ValueText(value, $"{what}: 'initialValue'");
            if (source.IsLinked)
            {
                return text;
            }

            return UnifiedTagDataTypes.IsArray(source.DataType!)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: an array takes no start value through this tool")
                : UnifiedTagValues.Constant(source.DataType!, text, $"{what}, start value");
        }

        private static uint? ReadMaxLength(JsonElement tag, UnifiedTagSource source, string what)
        {
            if (!tag.TryGetProperty("maxLength", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (source.DataType == null || !source.DataType.Equals("WString", StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'maxLength' is an internal WString's");
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var length) && length > 0
                ? length
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'maxLength' must be a whole number above 0; got {value.GetRawText()}");
        }

        private static UnifiedTagLimits ReadLimits(JsonElement tag, UnifiedTagSource source, string what)
        {
            if (!tag.TryGetProperty("limits", out var element) || element.ValueKind == JsonValueKind.Null)
            {
                return UnifiedTagLimits.None;
            }

            var limits = UnifiedTagJson.RequireObject(element, $"{what}: 'limits'");
            UnifiedTagJson.RequireKnownFields(limits, LimitFields, $"{what}: 'limits'");
            if (!source.IsLinked && !UnifiedTagValues.IsNumeric(source.DataType!))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: only a number tag has limits; a {source.DataType} has none");
            }

            var lower = ReadLimit(limits, "lower", source, what);
            var upper = ReadLimit(limits, "upper", source, what);
            if (lower == null && upper == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'limits' needs 'lower', 'upper' or both");
            }

            if (!source.IsLinked && lower is { IsTag: false } && upper is { IsTag: false })
            {
                UnifiedTagValues.RequireOrdered(lower.Value, upper.Value, what);
            }

            return new UnifiedTagLimits(lower, upper);
        }

        private static UnifiedTagLimit? ReadLimit(JsonElement limits, string side, UnifiedTagSource source, string what)
        {
            if (!limits.TryGetProperty(side, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return string.IsNullOrWhiteSpace(value.GetString())
                    ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a {side} limit that is a string names an HMI tag, and cannot be empty")
                    : UnifiedTagLimit.Tag(value.GetString()!.Trim());
            }

            if (value.ValueKind != JsonValueKind.Number)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a {side} limit is a number, or a string naming an HMI tag; got {value.GetRawText()}");
            }

            return UnifiedTagLimit.Constant(source.IsLinked ? value.GetRawText() : UnifiedTagValues.Constant(source.DataType!, value.GetRawText(), $"{what}, {side} limit"));
        }

        private static UnifiedTagPlcReading ReadPlcReading(JsonElement tag, UnifiedTagSource source, string what)
        {
            var reading = new UnifiedTagPlcReading(ReadAcquisition(tag, what), ReadScaling(tag, what), ReadSubstitute(tag, what));

            return !reading.IsSet || source.IsLinked
                ? reading
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'acquisition', 'scaling' and 'substitute' are a linked tag's; on an internal tag they are disabled fields");
        }

        private static UnifiedTagAcquisition? ReadAcquisition(JsonElement tag, string what)
        {
            var element = Optional(tag, "acquisition", AcquisitionFields, what);
            if (element == null)
            {
                return null;
            }

            var mode = UnifiedTagJson.OptionalString(element.Value, "mode") ?? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'acquisition' needs a 'mode': {string.Join(" or ", Modes.Keys)}");
            if (!Modes.TryGetValue(mode.Trim(), out var known))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: acquisition mode '{mode}' is not offered. Use {string.Join(" or ", Modes.Keys)}; cyclic continuous compiled as invalid on an S7-1200 connection");
            }

            var cycle = UnifiedTagJson.OptionalString(element.Value, "cycle");
            if (cycle != null && known == UnifiedAcquisitionMode.OnDemand)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a tag read on demand has no cycle");
            }

            return new UnifiedTagAcquisition(known, cycle == null ? null : UnifiedTagAcquisition.NormaliseCycle(cycle, what));
        }

        private static UnifiedTagScaling? ReadScaling(JsonElement tag, string what)
        {
            var element = Optional(tag, "scaling", ScalingFields, what);
            if (element == null)
            {
                return null;
            }

            var plc = UnifiedTagJson.NumberPair(element.Value, "plc", $"{what}: 'scaling'");
            var hmi = UnifiedTagJson.NumberPair(element.Value, "hmi", $"{what}: 'scaling'");

            return plc.Start == plc.End || hmi.Start == hmi.End
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a scaling range needs a start and an end that differ, which the compile requires")
                : new UnifiedTagScaling(plc, hmi);
        }

        private static UnifiedTagSubstitute? ReadSubstitute(JsonElement tag, string what)
        {
            var element = Optional(tag, "substitute", SubstituteFields, what);
            if (element == null)
            {
                return null;
            }

            var usage = UnifiedTagJson.OptionalString(element.Value, "usage");
            if (usage == null || !Usages.TryGetValue(usage.Trim(), out var known))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'substitute' needs a 'usage': {string.Join(", ", Usages.Keys)}");
            }

            return element.Value.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null
                ? new UnifiedTagSubstitute(known, UnifiedTagJson.ValueText(value, $"{what}: the substitute 'value'"))
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'substitute' needs a 'value'");
        }

        private static JsonElement? Optional(JsonElement tag, string field, IReadOnlyCollection<string> known, string what)
        {
            if (!tag.TryGetProperty(field, out var element) || element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var owner = UnifiedTagJson.RequireObject(element, $"{what}: '{field}'");
            UnifiedTagJson.RequireKnownFields(owner, known, $"{what}: '{field}'");

            return owner;
        }
    }
}
