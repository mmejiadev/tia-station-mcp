using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>What a WinCC Unified screen or item holds that its backup does not keep.</summary>
    /// <remarks>
    /// The backup holds only what a description draws: values, texts, points, tag links, an item's events
    /// and the animations <see cref="UnifiedDynamizationCoverage"/> keeps. Anything else is named here,
    /// and a screen that has it is not redrawn. Read by reflection over the Openness object, which is
    /// what lets the rule be tested without TIA Portal.
    /// </remarks>
    public static class UnifiedBackupCoverage
    {
        private const string EventHandlers = "EventHandlers";

        private const string PropertyEventHandlers = "PropertyEventHandlers";

        /// <summary>Lists what an object and its parts hold that the backup would lose.</summary>
        /// <param name="owner">The screen, an item, or one of their parts.</param>
        /// <param name="where">How a reason names the object, e.g. <c>'Tank'</c>.</param>
        /// <param name="eventsKept">
        /// True for an item: its event scripts are read into the backup since 2026-10-23 and drawn back.
        /// Property events, and a screen's own events, are not, and still count as lost.
        /// </param>
        /// <returns>One reason per thing lost; empty when the backup keeps everything.</returns>
        public static IReadOnlyList<string> Lost(object owner, string where, bool eventsKept = false)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var lost = new List<string>();
            AddLost(owner, where, lost, eventsKept, 0);

            return lost;
        }

        /// <remarks>
        /// Only the item's own events are read into the backup, so <paramref name="eventsKept"/> applies
        /// at the top level; a part's events, should one have any, still count as lost.
        /// </remarks>
        private static void AddLost(object owner, string where, List<string> lost, bool eventsKept, int depth)
        {
            var collections = eventsKept && depth == 0 ? new[] { PropertyEventHandlers } : new[] { EventHandlers, PropertyEventHandlers };

            foreach (var collection in collections.Where(name => Entries(owner, name).Any()))
            {
                lost.Add($"{where} has {collection} its backup does not keep");
            }

            foreach (var dynamization in Entries(owner, "Dynamizations"))
            {
                if (UnifiedDynamizationCoverage.Lost(dynamization) is string reason)
                {
                    lost.Add($"{where} has {reason} on {PropertyOf(dynamization)}, which its backup does not keep");
                }
            }

            if (depth >= UnifiedPropertyTree.MaxPartDepth)
            {
                return;
            }

            // A part that throws when read ends the call here, before anything was deleted.
            foreach (var part in owner.GetType().GetProperties().Where(property => UnifiedPropertyTree.IsPart(property.PropertyType)))
            {
                if (part.GetValue(owner) is object value)
                {
                    AddLost(value, $"{where}.{part.Name}", lost, eventsKept, depth + 1);
                }
            }
        }

        private static string PropertyOf(object dynamization)
        {
            return UnifiedPropertyTree.MostDerived(dynamization.GetType(), "PropertyName")?.GetValue(dynamization) as string ?? "a property";
        }

        /// <remarks>The most derived declaration only: see <see cref="UnifiedPropertyTree.MostDerived"/>.</remarks>
        private static IEnumerable<object> Entries(object owner, string collection)
        {
            return UnifiedPropertyTree.MostDerived(owner.GetType(), collection)?.GetValue(owner) is IEnumerable entries
                ? entries.Cast<object>()
                : Enumerable.Empty<object>();
        }
    }
}
