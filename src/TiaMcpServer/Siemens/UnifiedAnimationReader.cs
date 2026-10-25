using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Flashing;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Tag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads the animations of a WinCC Unified item in the shape <see cref="UnifiedAnimationListReader"/> takes.</summary>
    /// <remarks>
    /// Only what <see cref="UnifiedDynamizationCoverage"/> says the backup keeps is read; anything else is
    /// left out here and named by the backup check, so a screen holding it is not redrawn.
    /// </remarks>
    internal static class UnifiedAnimationReader
    {
        /// <summary>The text TIA Portal V20 puts in a new script dynamization's global definition area, left out when read back.</summary>
        private const string DefaultGlobalDefinitions = "//Insert definitions for script dynamizations here";

        /// <summary>Reads one dynamization as an animation.</summary>
        /// <param name="dynamization">The dynamization, which is not a plain tag link.</param>
        /// <param name="path">The animated property's path.</param>
        /// <returns>The animation, or null when the backup does not keep this kind.</returns>
        public static JsonObject? Read(DynamizationBase dynamization, string path)
        {
            if (UnifiedDynamizationCoverage.Lost(dynamization) != null)
            {
                return null;
            }

            return dynamization switch
            {
                TagDynamization link => ReadRanges(link, path),
                FlashingDynamization flashing => ReadFlashing(flashing, path),
                ScriptDynamization script => ReadScript(script, path),
                _ => null
            };
        }

        /// <summary>What in an item's script animations its backup could not draw back: an empty script, or one TIA Portal finds broken.</summary>
        /// <param name="dynamizations">The item's dynamizations, and its parts'.</param>
        /// <param name="itemName">The item, as a reason names it.</param>
        /// <returns>One reason per script that would not come back; empty when all would.</returns>
        public static IReadOnlyList<string> Problems(IEnumerable<DynamizationBase> dynamizations, string itemName)
        {
            var problems = new List<string>();

            foreach (var script in dynamizations.OfType<ScriptDynamization>())
            {
                if (string.IsNullOrWhiteSpace(script.ScriptCode))
                {
                    problems.Add($"'{itemName}' has an empty script on {script.PropertyName}, which a redraw cannot put back");
                    continue;
                }

                if (script.SyntaxCheck()?.Errors?.Any() == true)
                {
                    problems.Add($"'{itemName}' has a script on {script.PropertyName} TIA Portal finds broken, which a redraw cannot put back");
                }
            }

            return problems;
        }

        private static JsonObject ReadRanges(TagDynamization link, string path)
        {
            var ranges = new JsonArray();

            foreach (var entry in link.ValueConverter.MappingTable.Entries.Cast<MappingTableEntryRange>())
            {
                ranges.Add(ReadRange(entry));
            }

            return new JsonObject { ["property"] = path, ["tag"] = link.Tag, ["ranges"] = ranges };
        }

        /// <remarks>A range of one value is written without <c>to</c>, as a description gives it.</remarks>
        private static JsonObject ReadRange(MappingTableEntryRange entry)
        {
            var from = Convert.ToInt64(entry.From, System.Globalization.CultureInfo.InvariantCulture);
            var to = Convert.ToInt64(entry.To, System.Globalization.CultureInfo.InvariantCulture);
            var range = new JsonObject { ["from"] = from };

            if (to != from)
            {
                range["to"] = to;
            }

            range["value"] = Describe(entry.Value);

            if (entry.Flashing)
            {
                range["flashing"] = new JsonObject { ["rate"] = entry.FlashingRate.ToString(), ["alternate"] = Describe(entry.AlternateValue) };
            }

            return range;
        }

        private static JsonObject ReadFlashing(FlashingDynamization flashing, string path)
        {
            var described = new JsonObject
            {
                ["color"] = Describe(flashing.Color),
                ["alternate"] = Describe(flashing.AlternateColor),
                ["rate"] = flashing.FlashingRate.ToString(),
                ["condition"] = flashing.FlashingCondition.ToString()
            };

            return new JsonObject { ["property"] = path, ["flashing"] = described };
        }

        /// <remarks>The automatic trigger is TIA Portal's own default, so it is left out, as a description leaves it.</remarks>
        /// <summary>A script dynamization's global definitions, or null when they are empty or TIA Portal's default text.</summary>
        /// <param name="script">The script dynamization.</param>
        /// <returns>The definitions, or null.</returns>
        public static string? GlobalsOf(ScriptDynamization script)
        {
            var globals = script.GlobalDefinitionAreaScriptCode ?? string.Empty;

            return globals.Trim().Length > 0 && !string.Equals(globals.Trim(), DefaultGlobalDefinitions, StringComparison.Ordinal) ? globals : null;
        }

        private static JsonObject ReadScript(ScriptDynamization script, string path)
        {
            var read = new JsonObject { ["property"] = path, ["script"] = script.ScriptCode };

            var globals = GlobalsOf(script);
            if (globals != null)
            {
                read["globalDefinitions"] = globals;
            }

            switch (script.Trigger.Type)
            {
                case TriggerType.AutomaticTags:
                    break;
                case TriggerType.Tags:
                    read["trigger"] = new JsonArray(TriggerTags(script.Trigger).Select(tag => (JsonNode)JsonValue.Create(tag)!).ToArray());
                    break;
                default:
                    read["trigger"] = script.Trigger.Type.ToString();
                    break;
            }

            return read;
        }

        /// <remarks><c>Trigger.Tags</c> is typed <c>object</c>; it reads back as a <c>List&lt;string&gt;</c> (measured 2026-10-25).</remarks>
        private static IEnumerable<string> TriggerTags(Trigger trigger)
        {
            return trigger.Tags is IEnumerable<string> tags ? tags : Enumerable.Empty<string>();
        }

        private static JsonValue? Describe(object? value)
        {
            var described = UnifiedPropertyConverter.Describe(value);

            return described.HasValue ? JsonValue.Create(described.Value) : null;
        }
    }
}
