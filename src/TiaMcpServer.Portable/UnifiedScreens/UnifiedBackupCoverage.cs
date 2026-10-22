using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>What a WinCC Unified screen or item holds that its backup does not keep.</summary>
    /// <remarks>
    /// The backup holds only what a description draws: values, texts, points and tag links. Events,
    /// scripts and any dynamization other than a tag link are not in it, so they are named here, and a
    /// screen that has them is not redrawn. Read by reflection over the Openness object, which is what
    /// lets the rule be tested without TIA Portal. A dynamization is recognised as a tag link by its type
    /// name, so anything else — a script, a resource list, a type a later Openness adds — counts as lost.
    /// </remarks>
    public static class UnifiedBackupCoverage
    {
        private const string TagLink = "TagDynamization";

        private static readonly string[] EventCollections = { "EventHandlers", "PropertyEventHandlers" };

        /// <summary>Lists what an object and its parts hold that the backup would lose.</summary>
        /// <param name="owner">The screen, an item, or one of their parts.</param>
        /// <param name="where">How a reason names the object, e.g. <c>'Tank'</c>.</param>
        /// <returns>One reason per thing lost; empty when the backup keeps everything.</returns>
        public static IReadOnlyList<string> Lost(object owner, string where)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var lost = new List<string>();
            AddLost(owner, where, lost, 0);

            return lost;
        }

        private static void AddLost(object owner, string where, List<string> lost, int depth)
        {
            if (EventCollections.Any(name => Entries(owner, name).Any()))
            {
                lost.Add($"{where} has events or scripts");
            }

            if (Entries(owner, "Dynamizations").Any(dynamization => dynamization.GetType().Name != TagLink))
            {
                lost.Add($"{where} has a dynamization that is not a tag link (a script or a resource list)");
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
                    AddLost(value, $"{where}.{part.Name}", lost, depth + 1);
                }
            }
        }

        /// <remarks>
        /// Not <c>GetProperty(name)</c>: Openness item types declare <c>EventHandlers</c> again with their
        /// own event type, so reflection sees several properties of that name and the lookup throws an
        /// <see cref="System.Reflection.AmbiguousMatchException"/>. Nor every one of them: the hidden
        /// getter of a base type fails on the derived object — <c>HmiCircle.EventHandlers</c> read on an
        /// <c>HmiCircleSegment</c> cannot cast its composition (both measured 2026-10-21). The declaration
        /// of the most derived type is the one read.
        /// </remarks>
        private static IEnumerable<object> Entries(object owner, string collection)
        {
            var property = owner.GetType().GetProperties()
                .Where(candidate => candidate.Name == collection && candidate.GetIndexParameters().Length == 0)
                .OrderByDescending(candidate => InheritanceDepth(candidate.DeclaringType))
                .FirstOrDefault();

            return property?.GetValue(owner) is IEnumerable entries ? entries.Cast<object>() : Enumerable.Empty<object>();
        }

        private static int InheritanceDepth(Type? type)
        {
            var depth = 0;
            for (var current = type; current != null; current = current.BaseType)
            {
                depth++;
            }

            return depth;
        }
    }
}
