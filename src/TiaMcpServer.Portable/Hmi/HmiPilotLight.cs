using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A pilot light: a circle whose colour follows a Bool tag.</summary>
    /// <remarks>
    /// A shorthand for what the class projects draw by hand: a circle with an appearance animation of
    /// two ranges, 0 and 1, each with its own colour, written before any animation the frame adds.
    /// The teacher's alarm light also flashes at 1 and is visible only while its tag is set; both are
    /// options.
    /// </remarks>
    public sealed class HmiPilotLight : HmiScreenItem
    {
        /// <summary>Creates a pilot light.</summary>
        /// <param name="frame">Its name, box, look and further animations; the box's width is the diameter.</param>
        /// <param name="tag">The HMI tag it follows.</param>
        /// <param name="colors">Its colours at 0 and 1.</param>
        /// <param name="visibilityTag">A tag that shows the light only while it is 1, or empty for always visible.</param>
        public HmiPilotLight(HmiItemFrame frame, string tag, HmiPilotLightColors colors, string visibilityTag)
            : base(frame)
        {
            Tag = tag;
            Colors = colors;
            VisibilityTag = visibilityTag ?? string.Empty;
        }

        /// <summary>The HMI tag it follows.</summary>
        public string Tag { get; }

        /// <summary>Its colours at 0 and 1.</summary>
        public HmiPilotLightColors Colors { get; }

        /// <summary>A tag that shows the light only while it is 1, or empty.</summary>
        public string VisibilityTag { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var diameter = Frame.Box.Width;
            var style = HmiItemStyle.Circle();
            style["BackColor"] = Colors.OffColor;
            style["Radius"] = HmiScreenWriter.Number(diameter / 2);

            var circle = new HmiItemFrame(Frame.Name, new HmiBox(Frame.Box.Left, Frame.Box.Top, diameter, diameter), Frame.Look, Frame.Animations);
            var placed = HmiScreenWriter.Place(style, circle, HmiScreenWriter.NotFocusable);

            return writer.Item("Hmi.Screen.Circle", placed, () => OwnAnimations().Select(animation => animation.Write(writer))
                .Concat(WriteAnimations(writer))
                .ToList());
        }

        private List<HmiAnimation> OwnAnimations()
        {
            var ranges = new[]
            {
                new HmiRange(0, 0, HmiLook.Colored(Colors.OffColor), isFlashing: false),
                new HmiRange(1, 1, HmiLook.Colored(Colors.OnColor), Colors.FlashesWhenOn)
            };

            var animations = new List<HmiAnimation> { new HmiAppearanceAnimation(Tag, ranges, Colors.OffColor) };

            if (VisibilityTag.Length > 0)
            {
                animations.Add(new HmiVisibilityAnimation(VisibilityTag, 0, isVisibleWhenSet: true));
            }

            return animations;
        }
    }
}
