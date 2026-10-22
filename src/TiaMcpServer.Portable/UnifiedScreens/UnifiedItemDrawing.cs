using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>What an item carries besides its properties: its points and its tag links.</summary>
    public sealed class UnifiedItemDrawing
    {
        /// <summary>Creates the drawing.</summary>
        /// <param name="points">The points of a polyline or a polygon; empty for any other item.</param>
        /// <param name="tags">Property paths bound to an HMI tag.</param>
        public UnifiedItemDrawing(IReadOnlyList<UnifiedPoint> points, IReadOnlyDictionary<string, string> tags)
        {
            Points = points;
            Tags = tags;
        }

        /// <summary>The points.</summary>
        public IReadOnlyList<UnifiedPoint> Points { get; }

        /// <summary>The tag links.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }
    }
}
