using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Gives every event script, and every script animation, of a screen the screen's one global definition area.</summary>
    /// <remarks>
    /// The TIA Portal V21 help: each screen has two script contexts, one for its events and one for the dynamization of
    /// properties. Measured 2026-11-18 on V21: the global definitions an event is given are the screen's - two buttons
    /// given <c>let a = 1;</c> and <c>let b = 2;</c> both read back <c>let b = 2;</c>, and a button drawn after with
    /// none wiped the area for all of them; script animations share theirs the same way. Written item by item, the last
    /// one won without a word. So the definitions of a screen are merged - those already there first, then each given,
    /// once each - and every script is written with the whole. A part given that holds the whole area already there is
    /// taken as that area changed, and replaces it: how a definition is fixed or removed, by giving the area as read back.
    /// Parts are compared whole: two that declare the same name in other words are both kept, and TIA Portal's syntax check
    /// refuses the area, rather than one being dropped unseen.
    /// </remarks>
    public static class UnifiedScreenGlobals
    {
        /// <summary>The items, each event script and script animation carrying the merged global definitions of its context.</summary>
        /// <param name="items">The items to draw.</param>
        /// <param name="existing">The screen's global definitions already there: of its events, and of its animations; null for none.</param>
        /// <returns>The items, with the definitions shared; the same items when none has any.</returns>
        public static IReadOnlyList<UnifiedItemDescription> Shared(IReadOnlyList<UnifiedItemDescription> items, (string? Events, string? Animations) existing)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var events = Merge(existing.Events, items.SelectMany(item => item.Events).Select(script => script.GlobalDefinitions));
            var animations = Merge(existing.Animations, items.SelectMany(item => item.Animations).OfType<UnifiedScriptAnimation>().Select(script => script.GlobalDefinitions));

            return events == null && animations == null ? items : items.Select(item => WithGlobals(item, events, animations)).ToList();
        }

        /// <summary>Whether an item holds an event script: only then is the screen's global definition area written with the items.</summary>
        /// <param name="items">The items to draw.</param>
        /// <returns>True when some item has an event.</returns>
        /// <remarks>
        /// With none, the area is the screen's own events' and is left as it is: written anyway, it was overwritten with
        /// TIA Portal's default and those definitions were lost without a word (review of 2026-11-22).
        /// </remarks>
        public static bool HoldsEvents(IReadOnlyList<UnifiedItemDescription> items)
        {
            return (items ?? throw new ArgumentNullException(nameof(items))).Any(item => item.Events.Count > 0);
        }

        /// <summary>The global definitions the items' event scripts carry, once <see cref="Shared"/> has given each the whole.</summary>
        /// <param name="items">The items, after <see cref="Shared"/>.</param>
        /// <returns>The area; null when no script carries any.</returns>
        public static string? EventDefinitions(IReadOnlyList<UnifiedItemDescription> items)
        {
            return (items ?? throw new ArgumentNullException(nameof(items)))
                .SelectMany(item => item.Events)
                .Select(script => script.GlobalDefinitions)
                .FirstOrDefault(globals => globals != null);
        }

        /// <summary>Merges global definitions: the area already there, then each part given, each distinct part once.</summary>
        /// <param name="existing">The area already there, or null.</param>
        /// <param name="parts">The definitions given, some null or blank.</param>
        /// <returns>The merged definitions; null when there are none.</returns>
        public static string? Merge(string? existing, IEnumerable<string?> parts)
        {
            var given = (parts ?? throw new ArgumentNullException(nameof(parts))).Select(Normalised).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            var held = Normalised(existing);

            // A part holding the whole area is the area changed: it replaces the area rather than repeating it.
            var merged = held == null || given.Any(part => part.Contains(held)) ? given : new[] { held }.Concat(given.Where(part => part != held)).ToList();

            return merged.Count == 0 ? null : string.Join(Environment.NewLine, merged);
        }

        private static string? Normalised(string? part)
        {
            return string.IsNullOrWhiteSpace(part) ? null : part!.Replace("\r\n", "\n").Replace("\n", Environment.NewLine).Trim();
        }

        private static UnifiedItemDescription WithGlobals(UnifiedItemDescription item, string? events, string? animations)
        {
            var scripts = item.Events.Select(script => new UnifiedEventScript(script.EventName, script.Script, events)).ToList();
            var animated = item.Animations.Select(animation => animation is UnifiedScriptAnimation script
                ? new UnifiedScriptAnimation(script.Path, (script.Script, animations), script.Trigger, script.TriggerTags)
                : animation).ToList();

            return new UnifiedItemDescription(item.TypeName, item.Name, item.Properties, new UnifiedItemDrawing(item.Points, item.Tags, scripts, animated));
        }
    }
}
