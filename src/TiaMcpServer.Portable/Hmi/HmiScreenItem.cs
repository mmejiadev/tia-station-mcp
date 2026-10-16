using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>One item on a generated screen: a button, a field, a pilot light, a bar.</summary>
    /// <remarks>
    /// Each kind writes its own XML, so adding a kind is adding a class, not a branch in a switch
    /// that every other kind passes through. Animations are the frame's, common to every kind, and
    /// are written last among an item's children.
    /// </remarks>
    public abstract class HmiScreenItem
    {
        /// <summary>Creates the item.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        protected HmiScreenItem(HmiItemFrame frame)
        {
            Frame = frame;
        }

        /// <summary>Its name, box, look and animations.</summary>
        public HmiItemFrame Frame { get; }

        /// <summary>Writes the item as a screen item of a classic WinCC export.</summary>
        /// <param name="writer">The screen's writer.</param>
        /// <returns>The element.</returns>
        internal abstract XElement Write(HmiScreenWriter writer);

        /// <summary>The frame's animations, written.</summary>
        /// <param name="writer">The screen's writer.</param>
        /// <returns>One element per animation.</returns>
        internal IEnumerable<XElement> WriteAnimations(HmiScreenWriter writer)
        {
            return Frame.Animations.Select(animation => animation.Write(writer)).ToList();
        }

        /// <summary>The font to write: the look's overrides, or the style's.</summary>
        /// <param name="styleSize">The size the item's style uses.</param>
        /// <param name="styleFontStyle">The font style the item's style uses.</param>
        /// <returns>The font.</returns>
        protected HmiFont ResolveFont(int styleSize = HmiItemStyle.FontSize, string styleFontStyle = HmiItemStyle.FontStyle)
        {
            var font = Frame.Look.Font;

            return new HmiFont(font.Size > 0 ? font.Size : styleSize, font.Style.Length > 0 ? font.Style : styleFontStyle);
        }
    }
}
