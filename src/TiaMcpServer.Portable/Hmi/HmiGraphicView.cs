using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A graphic view: one of the project's pictures, stretched to the item's box.</summary>
    /// <remarks>
    /// The picture is linked by name, the way the class exports link it. The project's pictures are not
    /// reachable through Openness, so the name is checked against the panel's export before import — a
    /// picture no screen of the panel shows yet is refused rather than guessed at.
    /// </remarks>
    public sealed class HmiGraphicView : HmiScreenItem
    {
        /// <summary>Creates a graphic view.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="picture">The name of the picture it shows.</param>
        public HmiGraphicView(HmiItemFrame frame, string picture)
            : base(frame)
        {
            Picture = picture;
        }

        /// <summary>The name of the picture it shows.</summary>
        public string Picture { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var placed = HmiScreenWriter.Place(HmiItemStyle.GraphicView(), Frame, HmiScreenWriter.NotFocusable);
            var element = writer.Item("Hmi.Screen.GraphicView", placed, () => WriteAnimations(writer).ToList());

            // Right after the attributes, where the class exports have it, and before any animations.
            element.Element("AttributeList")!.AddAfterSelf(HmiScreenWriter.Link("Picture", Picture));

            return element;
        }
    }
}
