using System;

namespace TiaMcpServer.Hmi
{
    /// <summary>What a kind of screen item is called by default, its default size and colour, and how one is built.</summary>
    internal sealed class HmiItemKind
    {
        /// <summary>Creates a kind.</summary>
        /// <param name="namePrefix">What TIA Portal calls a new one, numbered after it: <c>Button</c> gives <c>Button_1</c>.</param>
        /// <param name="defaultSize">Its width and height when the description gives none; the position is unused.</param>
        /// <param name="styleBackColor">Its style's background, which an animation range without one keeps.</param>
        /// <param name="build">Builds one from its description, frame and the place to name in a refusal.</param>
        public HmiItemKind(string namePrefix, HmiBox defaultSize, string styleBackColor, Func<HmiScreenItemDocument, HmiItemFrame, string, HmiScreenItem> build)
        {
            NamePrefix = namePrefix;
            DefaultSize = defaultSize;
            StyleBackColor = styleBackColor;
            Build = build;
        }

        /// <summary>What TIA Portal calls a new one.</summary>
        public string NamePrefix { get; }

        /// <summary>Its width and height when the description gives none.</summary>
        public HmiBox DefaultSize { get; }

        /// <summary>Its style's background.</summary>
        public string StyleBackColor { get; }

        /// <summary>Builds one from its description, frame and the place to name in a refusal.</summary>
        public Func<HmiScreenItemDocument, HmiItemFrame, string, HmiScreenItem> Build { get; }
    }
}
