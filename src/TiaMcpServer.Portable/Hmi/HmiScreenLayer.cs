using System.Collections.Generic;

namespace TiaMcpServer.Hmi
{
    /// <summary>One layer of a classic WinCC screen and the items drawn on it.</summary>
    /// <remarks>
    /// A classic screen has 32 layers, 0 to 31, and every export of the class projects writes the ones
    /// in use as <c>Hmi.Screen.ScreenLayer</c> with an empty name; an item belongs to exactly one.
    /// </remarks>
    public sealed class HmiScreenLayer
    {
        /// <summary>The highest layer index a classic screen has.</summary>
        public const int MaxIndex = 31;

        /// <summary>Creates a layer.</summary>
        /// <param name="index">Its index, 0 to <see cref="MaxIndex"/>.</param>
        /// <param name="items">Its items, in drawing and tab order.</param>
        public HmiScreenLayer(int index, IReadOnlyList<HmiScreenItem> items)
        {
            Index = index;
            Items = items;
        }

        /// <summary>Its index.</summary>
        public int Index { get; }

        /// <summary>Its items.</summary>
        public IReadOnlyList<HmiScreenItem> Items { get; }
    }
}
