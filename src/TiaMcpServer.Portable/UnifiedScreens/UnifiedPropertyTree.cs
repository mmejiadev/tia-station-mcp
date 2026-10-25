using System;
using System.Collections;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Which properties of a WinCC Unified item a description reaches, and how deep.</summary>
    /// <remarks>
    /// One definition for the three places that walk an item — the catalogue that lists its properties,
    /// the reader that backs a screen up, and the setting a description writes — because a screen read
    /// back has to be drawable again: a path the reader writes and the setting refuses, or the other way
    /// round, would break the backup.
    /// </remarks>
    public static class UnifiedPropertyTree
    {
        /// <summary>The namespace of an item's parts: <c>Font</c>, <c>Padding</c>, <c>Corners</c>.</summary>
        public const string PartsNamespace = "Siemens.Engineering.HmiUnified.UI.Parts";

        /// <summary>How many parts deep a path goes: <c>Font.Size</c> is one, so a path has at most three segments.</summary>
        public const int MaxPartDepth = 2;

        /// <summary>The most segments a property path has.</summary>
        public const int MaxSegments = MaxPartDepth + 1;

        /// <summary>The points of a polyline or a polygon, which a description gives in its own field.</summary>
        public const string Points = "Points";

        private static readonly HashSet<string> NotProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name", Points, "Parent", "Dynamizations", "EventHandlers", "PropertyEventHandlers", "ScreenItems"
        };

        /// <summary>Whether a top-level name is set through its own field or not at all, rather than through <c>properties</c>.</summary>
        /// <param name="name">The property's name.</param>
        /// <returns>True for the name, the points, the tag links, the events, the children and the owner.</returns>
        public static bool IsNotAProperty(string name)
        {
            return NotProperties.Contains(name ?? string.Empty);
        }

        /// <summary>The declaration of a property that the object's own type makes, when several types in its hierarchy declare one.</summary>
        /// <param name="type">The object's type.</param>
        /// <param name="name">The property's name.</param>
        /// <returns>The most derived declaration, or null when the type has none.</returns>
        /// <remarks>
        /// Openness item types declare <c>EventHandlers</c> again with their own event type, so
        /// <c>GetProperty(name)</c> throws an ambiguous match, and the hidden getter of a base type fails on
        /// the derived object (<c>HmiCircle.EventHandlers</c> on an <c>HmiCircleSegment</c>, measured
        /// 2026-10-21). The one declaration to read is the most derived.
        /// </remarks>
        public static System.Reflection.PropertyInfo? MostDerived(Type type, string name)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            System.Reflection.PropertyInfo? found = null;
            var foundDepth = -1;

            foreach (var property in type.GetProperties())
            {
                var depth = Depth(property.DeclaringType);
                if (property.Name == name && property.GetIndexParameters().Length == 0 && depth > foundDepth)
                {
                    found = property;
                    foundDepth = depth;
                }
            }

            return found;
        }

        private static int Depth(Type? type)
        {
            var depth = 0;
            for (var current = type; current != null; current = current.BaseType)
            {
                depth++;
            }

            return depth;
        }

        /// <summary>Whether a property's type is a part, walked into rather than set as a value.</summary>
        /// <param name="type">The property's type.</param>
        /// <returns>True for a part; false for a value or a list of parts.</returns>
        public static bool IsPart(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.Namespace == PartsNamespace && !typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}
