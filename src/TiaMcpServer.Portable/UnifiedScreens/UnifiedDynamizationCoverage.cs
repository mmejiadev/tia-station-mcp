using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Which dynamizations of a WinCC Unified item its backup keeps, and why one is not kept.</summary>
    /// <remarks>
    /// The backup keeps a tag link with no table, a range table (<see cref="UnifiedRangeAnimation"/>), a
    /// flashing colour and a script on a named trigger: what a description draws. Anything else — a
    /// single-bit, bitmask or expression table, a formula, indirect addressing, a resource list, a
    /// custom cycle, a type a later Openness adds — is named. Read by reflection over the Openness
    /// object, by type and property names, so the rule is tested without TIA Portal.
    /// </remarks>
    public static class UnifiedDynamizationCoverage
    {
        private const string TagLink = "TagDynamization";
        private const string Flashing = "FlashingDynamization";
        private const string Script = "ScriptDynamization";
        private const string RangeEntry = "MappingTableEntryRange";
        private const string NoTable = "None";
        private const string RangeTable = "Range";

        /// <summary>The triggers a backup draws back, as Openness V20 names them in <c>TriggerType</c>; any other counts as lost.</summary>
        private static readonly HashSet<string> KeptTriggers = new HashSet<string>(StringComparer.Ordinal)
        {
            "AutomaticTags", "Tags", "Disabled", "T100ms", "T250ms", "T500ms", "T1s", "T2s", "T5s", "T10s"
        };

        /// <summary>The bound types TIA Portal V20 takes (measured 2026-10-25); a bound of any other type is not drawn back as it is.</summary>
        private static readonly HashSet<Type> WholeBounds = new HashSet<Type> { typeof(short), typeof(int), typeof(long) };

        /// <summary>Why the backup would lose a dynamization, or null when it keeps it.</summary>
        /// <param name="dynamization">An Openness dynamization.</param>
        /// <returns>What is lost, e.g. <c>a Singlebit table</c>; null when nothing is.</returns>
        public static string? Lost(object dynamization)
        {
            var typeName = dynamization?.GetType().Name ?? string.Empty;

            return typeName switch
            {
                TagLink => LostFromTagLink(dynamization!),
                Flashing => null,
                Script => LostFromScript(dynamization!),
                _ => $"a {typeName}"
            };
        }

        /// <summary>Whether a tag link is a plain one, with no table and no formula: what <c>tags</c> draws.</summary>
        /// <param name="link">An Openness <c>TagDynamization</c>.</param>
        /// <returns>True for a plain link.</returns>
        public static bool IsPlainLink(object link)
        {
            var table = Read(Read(link, "ValueConverter"), "MappingTable");

            return LostFromTagLink(link) == null && Read(table, "ConditionType")?.ToString() != RangeTable;
        }

        private static string? LostFromTagLink(object link)
        {
            if (Read(link, "UseIndirectAddressing") is true)
            {
                return "a tag link with indirect addressing";
            }

            var converter = Read(link, "ValueConverter");
            if (Read(converter, "IsFormulaSelected") is true)
            {
                return "a tag link with a formula";
            }

            var table = Read(converter, "MappingTable");
            var condition = Read(table, "ConditionType")?.ToString() ?? NoTable;
            var entries = Read(table, "Entries") is IEnumerable listed ? listed.Cast<object>().ToList() : new List<object>();

            return condition switch
            {
                NoTable when entries.Count == 0 => null,
                RangeTable when entries.Count > 0 && entries.All(IsDrawableRange) => null,
                RangeTable when entries.Count == 0 => "an empty range table",
                _ => $"a tag link with a {condition} table"
            };
        }

        /// <remarks>
        /// A range is drawn back from whole-number bounds, and one that flashes with its alternate colour;
        /// a bound of another type (a fractional one, or none) would be changed on the way back.
        /// </remarks>
        private static bool IsDrawableRange(object entry)
        {
            return entry.GetType().Name == RangeEntry
                && Read(entry, "From") is object from && WholeBounds.Contains(from.GetType())
                && Read(entry, "To") is object to && WholeBounds.Contains(to.GetType())
                && (Read(entry, "Flashing") is not true || Read(entry, "AlternateValue") is System.Drawing.Color);
        }

        private static string? LostFromScript(object script)
        {
            var trigger = Read(Read(script, "Trigger"), "Type")?.ToString() ?? string.Empty;

            return KeptTriggers.Contains(trigger) ? null : $"a script run on the trigger '{trigger}'";
        }

        private static object? Read(object? owner, string name)
        {
            return owner == null ? null : UnifiedPropertyTree.MostDerived(owner.GetType(), name)?.GetValue(owner);
        }
    }
}
