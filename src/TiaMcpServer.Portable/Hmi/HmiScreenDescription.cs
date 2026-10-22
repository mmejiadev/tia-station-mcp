using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A screen to generate: its name, number, background and items, layer by layer.</summary>
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
        /// <param name="layers">The layers in use, lowest index first, each with its items.</param>
        public HmiScreenDescription(string name, int? number, string backColor, IReadOnlyList<HmiScreenLayer> layers)
        {
            Name = name;
            Number = number;
            BackColor = backColor;
            Layers = layers;
        }

        /// <summary>The screen's name.</summary>
        public string Name { get; }

        /// <summary>The screen's number, or null for TIA Portal to choose a free one.</summary>
        public int? Number { get; }

        /// <summary>Background colour as <c>R, G, B</c>.</summary>
        public string BackColor { get; }

        /// <summary>The layers in use, lowest index first.</summary>
        public IReadOnlyList<HmiScreenLayer> Layers { get; }

        /// <summary>Every item on the screen's top level, layer by layer, in drawing and tab order.</summary>
        public IReadOnlyList<HmiScreenItem> Items => Layers.SelectMany(layer => layer.Items).ToList();
    }
}
