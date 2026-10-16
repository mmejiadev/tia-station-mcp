using System.Collections.Generic;

namespace TiaMcpServer.Hmi
{
    /// <summary>A screen to generate: its name, number, background and items.</summary>
    /// <remarks>
    /// The size is left out on purpose. A screen takes the size of its panel, and writing one that
    /// does not match would put the items somewhere else than where they were described.
    /// </remarks>
    public sealed class HmiScreenDescription
    {
        /// <summary>Creates a description.</summary>
        /// <param name="name">The screen's name, which buttons navigate to.</param>
        /// <param name="number">The screen's number, unique on the panel, or null for TIA Portal to choose a free one.</param>
        /// <param name="backColor">Background colour as <c>R, G, B</c>.</param>
        /// <param name="items">The items, in drawing and tab order.</param>
        public HmiScreenDescription(string name, int? number, string backColor, IReadOnlyList<HmiScreenItem> items)
        {
            Name = name;
            Number = number;
            BackColor = backColor;
            Items = items;
        }

        /// <summary>The screen's name.</summary>
        public string Name { get; }

        /// <summary>The screen's number, or null for TIA Portal to choose a free one.</summary>
        public int? Number { get; }

        /// <summary>Background colour as <c>R, G, B</c>.</summary>
        public string BackColor { get; }

        /// <summary>The items, in drawing and tab order.</summary>
        public IReadOnlyList<HmiScreenItem> Items { get; }
    }
}
