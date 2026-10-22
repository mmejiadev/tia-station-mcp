using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A plain circle, without the tag a pilot light follows; its box's width is the diameter.</summary>
    public sealed class HmiCircle : HmiScreenItem
    {
        /// <summary>Creates a circle.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        public HmiCircle(HmiItemFrame frame)
            : base(frame)
        {
        }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var diameter = Frame.Box.Width;
            var style = HmiItemStyle.Circle();
            style["BackColor"] = Frame.Look.BackColor.Length > 0 ? Frame.Look.BackColor : HmiItemStyle.Rectangle()["BackColor"];
            style["Radius"] = HmiScreenWriter.Number(diameter / 2);

            var circle = new HmiItemFrame(Frame.Name, new HmiBox(Frame.Box.Left, Frame.Box.Top, diameter, diameter), Frame.Look, Frame.Animations);
            var placed = HmiScreenWriter.Place(style, circle, HmiScreenWriter.NotFocusable);

            return writer.Item("Hmi.Screen.Circle", placed, () => WriteAnimations(writer).ToList());
        }
    }
}
