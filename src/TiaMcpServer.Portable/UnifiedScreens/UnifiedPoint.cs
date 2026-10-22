namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>One point of a polyline or a polygon, relative to the item.</summary>
    public sealed class UnifiedPoint
    {
        /// <summary>Creates a point.</summary>
        /// <param name="x">Horizontal position.</param>
        /// <param name="y">Vertical position.</param>
        public UnifiedPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Horizontal position.</summary>
        public int X { get; }

        /// <summary>Vertical position.</summary>
        public int Y { get; }
    }
}
