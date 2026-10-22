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
