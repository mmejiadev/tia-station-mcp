namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>Where a faceplate container goes on its screen, and its size.</summary>
    /// <remarks>A container is created at the size its type was drawn at (250 x 250 measured 2026-10-30); a size given here replaces it.</remarks>
    public sealed class UnifiedFaceplateFrame
    {
        /// <summary>Creates a frame.</summary>
        /// <param name="left">Distance from the screen's left edge.</param>
        /// <param name="top">Distance from the screen's top edge.</param>
        /// <param name="width">The width, or null for the type's own.</param>
        /// <param name="height">The height, or null for the type's own.</param>
        public UnifiedFaceplateFrame(int left, int top, uint? width, uint? height)
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

        /// <summary>The width, or null for the type's own.</summary>
        public uint? Width { get; }

        /// <summary>The height, or null for the type's own.</summary>
        public uint? Height { get; }
    }
}
