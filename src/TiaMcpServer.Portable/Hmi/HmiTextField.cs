using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A static text: a label next to a pilot light, a title.</summary>
    public sealed class HmiTextField : HmiScreenItem
    {
        /// <summary>Creates a text field.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="text">The text.</param>
        public HmiTextField(HmiItemFrame frame, string text)
            : base(frame)
        {
            Text = text;
        }

        /// <summary>The text.</summary>
        public string Text { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var placed = HmiScreenWriter.Place(HmiItemStyle.TextField(), Frame, HmiScreenWriter.NotFocusable);

            return writer.Item("Hmi.Screen.TextField", placed, () => new[] { writer.Font(ResolveFont()), writer.Text("Text", Text) }
                .Concat(WriteAnimations(writer))
                .ToList());
        }
    }
}
