using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>One WinCC Unified screen item to draw.</summary>
    public sealed class UnifiedItemDescription
    {
        /// <summary>Creates an item description.</summary>
        /// <param name="typeName">The full Openness type name, from <see cref="UnifiedItemTypes.Resolve"/>.</param>
        /// <param name="name">The item's name, unique on the screen.</param>
        /// <param name="properties">The properties to set, in the order given.</param>
        /// <param name="drawing">The item's points and tag links.</param>
        public UnifiedItemDescription(string typeName, string name, IReadOnlyList<UnifiedPropertySetting> properties, UnifiedItemDrawing drawing)
        {
            if (drawing == null)
            {
                throw new ArgumentNullException(nameof(drawing));
            }

            TypeName = typeName;
            Name = name;
            Properties = properties;
            Points = drawing.Points;
            Tags = drawing.Tags;
            Events = drawing.Events;
        }

        /// <summary>One script per event; empty when the item has none.</summary>
        public IReadOnlyList<UnifiedEventScript> Events { get; }

        /// <summary>The full Openness type name.</summary>
        public string TypeName { get; }

        /// <summary>The item's name.</summary>
        public string Name { get; }

        /// <summary>The properties to set.</summary>
        public IReadOnlyList<UnifiedPropertySetting> Properties { get; }

        /// <summary>The points of a polyline or a polygon; empty for any other item.</summary>
        public IReadOnlyList<UnifiedPoint> Points { get; }

        /// <summary>Property paths bound to an HMI tag: <c>ProcessValue</c> → <c>Level</c>.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }
    }
}
