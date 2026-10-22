using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A rectangle: a tank, a frame around a group of controls, a background panel.</summary>
    public sealed class HmiRectangle : HmiScreenItem
    {
        /// <summary>Creates a rectangle.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        public HmiRectangle(HmiItemFrame frame)
            : base(frame)
        {
        }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var placed = HmiScreenWriter.Place(HmiItemStyle.Rectangle(), Frame, HmiScreenWriter.NotFocusable);

            return writer.Item("Hmi.Screen.Rectangle", placed, () => WriteAnimations(writer).ToList());
        }
    }
}
