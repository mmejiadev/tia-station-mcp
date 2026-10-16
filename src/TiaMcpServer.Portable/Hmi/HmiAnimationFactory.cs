using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>Builds an item's animations from their description, refusing one that is incomplete.</summary>
    /// <remarks>
    /// Two kinds, both measured in the class exports: <c>appearance</c>, colours by value range, and
    /// <c>visibility</c>, shown or hidden by one bit. One of each per item: the class exports never
    /// carry two of a kind on one object, and every range goes in the one appearance animation.
    /// <code>
    /// "animations": [
    ///   { "type": "appearance", "tag": "Level", "ranges": [
    ///       { "from": 0, "to": 79, "backColor": "green" },
    ///       { "from": 80, "to": 100, "backColor": "red", "foreColor": "white", "flashing": true } ] },
    ///   { "type": "visibility", "tag": "Manual", "bit": 0, "visible": true } ]
    /// </code>
    /// </remarks>
    internal static class HmiAnimationFactory
    {
        private const int MaxBit = 31;

        private static readonly Dictionary<string, Func<HmiAnimationDocument, string, string, HmiAnimation>> Kinds =
            new Dictionary<string, Func<HmiAnimationDocument, string, string, HmiAnimation>>(StringComparer.OrdinalIgnoreCase)
            {
                [HmiAppearanceAnimation.KindName] = BuildAppearance,
                [HmiVisibilityAnimation.KindName] = BuildVisibility
            };

        /// <summary>Builds the animations of one item.</summary>
        /// <param name="documents">Their descriptions; null for none.</param>
        /// <param name="itemBackColor">The item's background, for a range that names none.</param>
        /// <param name="where">The item, named in a refusal.</param>
        /// <returns>The animations, possibly none.</returns>
        public static List<HmiAnimation> Read(IReadOnlyList<HmiAnimationDocument>? documents, string itemBackColor, string where)
        {
            var animations = (documents ?? new List<HmiAnimationDocument>())
                .Select((document, index) => Build(document, itemBackColor, $"{where}, animation {index + 1}"))
                .ToList();

            var repeated = animations.GroupBy(animation => animation.Kind).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

            return repeated.Count == 0
                ? animations
                : throw HmiRefusal.Refuse(where, $"one animation of each kind per item; repeated: {string.Join(", ", repeated)}. Put every range in one appearance animation");
        }

        private static HmiAnimation Build(HmiAnimationDocument document, string itemBackColor, string where)
        {
            return Kinds.TryGetValue((document.Type ?? string.Empty).Trim(), out var build)
                ? build(document, itemBackColor, where)
                : throw HmiRefusal.Refuse(where, $"'type' must be one of: {string.Join(", ", Kinds.Keys)}");
        }

        private static HmiAppearanceAnimation BuildAppearance(HmiAnimationDocument document, string itemBackColor, string where)
        {
            var tag = HmiRefusal.RequireText(document.Tag, where, "tag");
            var ranges = (document.Ranges ?? new List<HmiRangeDocument>())
                .Select((range, index) => BuildRange(range, $"{where}, range {index + 1}"))
                .ToList();

            if (ranges.Count == 0)
            {
                throw HmiRefusal.Refuse(where, "an appearance animation needs at least one range");
            }

            return new HmiAppearanceAnimation(tag, ranges, itemBackColor);
        }

        private static HmiRange BuildRange(HmiRangeDocument range, string where)
        {
            var from = range.From ?? throw HmiRefusal.Refuse(where, "'from' is required");
            var to = range.To ?? from;

            if (to < from)
            {
                throw HmiRefusal.Refuse(where, "'to' must not be below 'from'");
            }

            var look = new HmiLook(Color(range.BackColor, where, "backColor"), Color(range.ForeColor, where, "foreColor"), HmiFont.Unset, string.Empty);

            return new HmiRange(from, to, look, range.Flashing ?? false);
        }

        private static HmiVisibilityAnimation BuildVisibility(HmiAnimationDocument document, string itemBackColor, string where)
        {
            var bit = document.Bit ?? 0;

            return bit >= 0 && bit <= MaxBit
                ? new HmiVisibilityAnimation(HmiRefusal.RequireText(document.Tag, where, "tag"), bit, document.Visible ?? true)
                : throw HmiRefusal.Refuse(where, $"'bit' must be between 0 and {MaxBit}");
        }

        private static string Color(string? text, string where, string field)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : HmiColor.Parse(text!, $"{where}, {field}");
        }
    }
}
