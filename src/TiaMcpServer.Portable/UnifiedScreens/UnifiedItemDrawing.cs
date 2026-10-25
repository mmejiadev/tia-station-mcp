using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>What an item carries besides its properties: its points, its tag links, its events and its animations.</summary>
    public sealed class UnifiedItemDrawing
    {
        /// <summary>Creates the drawing.</summary>
        /// <param name="points">The points of a polyline or a polygon; empty for any other item.</param>
        /// <param name="tags">Property paths bound to an HMI tag.</param>
        /// <param name="events">One script per event; empty when the item has none.</param>
        /// <param name="animations">One animation per animated property; empty when the item has none.</param>
        public UnifiedItemDrawing(IReadOnlyList<UnifiedPoint> points, IReadOnlyDictionary<string, string> tags, IReadOnlyList<UnifiedEventScript> events, IReadOnlyList<UnifiedAnimation> animations)
        {
            Points = points;
            Tags = tags;
            Events = events;
            Animations = animations;
        }

        /// <summary>The animations, one per property.</summary>
        public IReadOnlyList<UnifiedAnimation> Animations { get; }

        /// <summary>The events, one script each.</summary>
        public IReadOnlyList<UnifiedEventScript> Events { get; }

        /// <summary>The points.</summary>
        public IReadOnlyList<UnifiedPoint> Points { get; }

        /// <summary>The tag links.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }
    }
}
