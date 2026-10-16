namespace TiaMcpServer.Hmi
{
    /// <summary>Where a screen item sits and how big it is, in panel pixels.</summary>
    public sealed class HmiBox
    {
        /// <summary>Creates a box.</summary>
        /// <param name="left">Distance from the screen's left edge.</param>
        /// <param name="top">Distance from the screen's top edge.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public HmiBox(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        /// <summary>Distance from the screen's left edge.</summary>
        public int Left { get; }

        /// <summary>Distance from the screen's top edge.</summary>
        public int Top { get; }

        /// <summary>Width.</summary>
        public int Width { get; }

        /// <summary>Height.</summary>
        public int Height { get; }
    }
}
