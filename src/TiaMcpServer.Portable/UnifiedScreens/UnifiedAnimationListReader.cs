using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Reads an item's <c>animations</c>: one entry per animated property.</summary>
    /// <remarks>
    /// Four kinds, told apart by the field each one alone has:
    /// <code>
    /// "animations": [
    ///   { "property": "BackColor", "tag": "Motor", "ranges": [ { "from": 0, "value": "red" }, { "from": 1, "value": "green" } ] },
    ///   { "property": "BackColor", "tag": "Alarm", "ranges": [ { "from": 1, "value": "red", "flashing": { "rate": "fast", "alternate": "white" } } ] },
    ///   { "property": "BorderColor", "flashing": { "color": "red", "alternate": "gray", "rate": "fast", "condition": "always" } },
    ///   { "property": "BackColor", "script": "return Tags(\"Motor\").Read() ? 0xFF00C800 : 0xFFFF0000;", "trigger": ["Motor"] },
    ///   { "property": "Text", "tag": "State", "list": "States" } ]
    /// </code>
    /// A field that belongs to another kind is refused rather than ignored, and so is a property animated
    /// twice. Whether the item's type has the property, and what it takes, is checked when it is drawn.
    /// </remarks>
    public static class UnifiedAnimationListReader
    {
        private const string RangesField = "ranges";
        private const string FlashingField = "flashing";
        private const string ScriptField = "script";
        private const string ListField = "list";

        private static readonly Dictionary<string, string[]> FieldsOfKind = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [RangesField] = new[] { "property", "tag", RangesField },
            [FlashingField] = new[] { "property", FlashingField },
            [ScriptField] = new[] { "property", ScriptField, "globalDefinitions", "trigger" },
            [ListField] = new[] { "property", "tag", ListField }
        };

        private static readonly string[] FlashingFields = { "color", "alternate", "rate", "condition" };

        /// <summary>Reads the animations of an item, which it may have none of.</summary>
        /// <param name="item">The item's JSON.</param>
        /// <param name="what">The item, as a refusal names it.</param>
        /// <returns>The animations, in the order given.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static IReadOnlyList<UnifiedAnimation> Read(JsonElement item, string what)
        {
            if (!item.TryGetProperty("animations", out var animations) || animations.ValueKind == JsonValueKind.Null)
            {
                return Array.Empty<UnifiedAnimation>();
            }

            if (animations.ValueKind != JsonValueKind.Array)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'animations' must be a list");
            }

            var read = animations.EnumerateArray().Select((entry, index) => ReadAnimation(entry, $"{what}, animation {index + 1}")).ToList();
            var repeated = read.GroupBy(animation => animation.Path, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? read
                : throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"{what} animates {string.Join(", ", repeated)} twice: a property holds one animation, and TIA Portal replaces the first with the second");
        }

        private static UnifiedAnimation ReadAnimation(JsonElement entry, string what)
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
            }

            var kinds = FieldsOfKind.Keys.Where(kind => entry.TryGetProperty(kind, out _)).ToList();
            if (kinds.Count != 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs exactly one of 'ranges' (with 'tag'), 'flashing', 'script' or 'list' (with 'tag')");
            }

            var kind = kinds[0];
            RequireOnly(entry, FieldsOfKind[kind], what, kind);
            var path = UnifiedPropertySetting.NormalisePath(RequireText(entry, "property", what));

            return kind switch
            {
                RangesField => new UnifiedRangeAnimation(path, RequireText(entry, "tag", what).Trim(), UnifiedRangeListReader.Read(entry.GetProperty(RangesField), what)),
                FlashingField => ReadFlashing(path, entry.GetProperty(FlashingField), what),
                ScriptField => ReadScript(path, entry, what),
                ListField => new UnifiedListAnimation(path, RequireText(entry, "tag", what).Trim(), RequireText(entry, ListField, what).Trim()),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised animation kind: {kind}")
            };
        }

        private static UnifiedFlashingAnimation ReadFlashing(string path, JsonElement flashing, string what)
        {
            if (flashing.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'flashing' must be an object with 'color' and 'alternate'");
            }

            RequireOnly(flashing, FlashingFields, $"{what}: 'flashing'", FlashingField);
            var colors = (Require(flashing, "color", what), Require(flashing, "alternate", what));
            var rate = OneOf(flashing, "rate", UnifiedFlashingAnimation.Rates, "Medium", what);
            var condition = OneOf(flashing, "condition", UnifiedFlashingAnimation.Conditions, "Always", what);

            return new UnifiedFlashingAnimation(path, colors, rate, condition);
        }

        private static UnifiedScriptAnimation ReadScript(string path, JsonElement entry, string what)
        {
            var script = RequireText(entry, ScriptField, what);
            var globals = entry.TryGetProperty("globalDefinitions", out var global) && global.ValueKind != JsonValueKind.Null
                ? (global.ValueKind == JsonValueKind.String ? global.GetString() : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'globalDefinitions' must be JavaScript, as text"))
                : null;

            if (!entry.TryGetProperty("trigger", out var trigger) || trigger.ValueKind == JsonValueKind.Null)
            {
                return new UnifiedScriptAnimation(path, (script, globals), UnifiedScriptAnimation.AutomaticTrigger, Array.Empty<string>());
            }

            return trigger.ValueKind == JsonValueKind.Array
                ? new UnifiedScriptAnimation(path, (script, globals), UnifiedScriptAnimation.TagsTrigger, ReadTriggerTags(trigger, what))
                : new UnifiedScriptAnimation(path, (script, globals), OneOf(entry, "trigger", UnifiedScriptAnimation.NamedTriggers, UnifiedScriptAnimation.AutomaticTrigger, what), Array.Empty<string>());
        }

        private static List<string> ReadTriggerTags(JsonElement trigger, string what)
        {
            var tags = trigger.EnumerateArray().Select(tag => tag.ValueKind == JsonValueKind.String ? tag.GetString()!.Trim() : string.Empty).ToList();

            return tags.Count > 0 && tags.All(tag => tag.Length > 0)
                ? tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: a 'trigger' list names the HMI tags that run the script, at least one");
        }

        /// <summary>Reads a field that names one of a few values, case aside, refusing anything else.</summary>
        /// <param name="owner">The JSON object.</param>
        /// <param name="field">The field.</param>
        /// <param name="names">The values, spelled as they are returned.</param>
        /// <param name="fallback">The value when the field is absent.</param>
        /// <param name="what">The owner, as a refusal names it.</param>
        /// <returns>The value, spelled as in <paramref name="names"/>.</returns>
        internal static string OneOf(JsonElement owner, string field, IReadOnlyList<string> names, string fallback, string what)
        {
            if (!owner.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return fallback;
            }

            var given = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;

            return names.FirstOrDefault(name => name.Equals(given, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' is one of {string.Join(", ", names)}; got {value.GetRawText()}");
        }

        private static void RequireOnly(JsonElement owner, IReadOnlyCollection<string> allowed, string what, string kind)
        {
            var stray = owner.EnumerateObject().Select(property => property.Name).Where(name => !allowed.Contains(name)).ToList();

            if (stray.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: {string.Join(", ", stray)} {(stray.Count == 1 ? "is" : "are")} not part of a '{kind}' animation, so it would be lost");
            }
        }

        private static JsonElement Require(JsonElement owner, string field, string what)
        {
            return owner.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null
                ? value
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' is required");
        }

        private static string RequireText(JsonElement owner, string field, string what)
        {
            var value = owner.TryGetProperty(field, out var given) && given.ValueKind == JsonValueKind.String ? given.GetString() : null;

            return !string.IsNullOrWhiteSpace(value)
                ? value!
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' is required, as text");
        }
    }
}
