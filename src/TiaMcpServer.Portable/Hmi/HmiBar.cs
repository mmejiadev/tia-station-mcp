using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A vertical bar showing a numeric tag against a scale: a level, a temperature.</summary>
    public sealed class HmiBar : HmiScreenItem
    {
        /// <summary>Creates a bar.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="tag">The HMI tag it shows.</param>
        /// <param name="minimum">The bottom of the scale.</param>
        /// <param name="maximum">The top of the scale.</param>
        public HmiBar(HmiItemFrame frame, string tag, double minimum, double maximum)
            : base(frame)
        {
            Tag = tag;
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>The HMI tag it shows.</summary>
        public string Tag { get; }

        /// <summary>The bottom of the scale.</summary>
        public double Minimum { get; }

        /// <summary>The top of the scale.</summary>
        public double Maximum { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var style = HmiItemStyle.Bar();
            style["MinimumValue"] = HmiScreenWriter.Number(Minimum);
            style["MaximumValue"] = HmiScreenWriter.Number(Maximum);

            var placed = HmiScreenWriter.Place(style, Frame, HmiScreenWriter.NotFocusable);

            return writer.Item("Hmi.Screen.Bar", placed, () => new[] { writer.Font(ResolveFont(HmiItemStyle.BarFontSize)), writer.ProcessValue(Tag) }
                .Concat(WriteAnimations(writer))
                .ToList());
        }
    }
}
