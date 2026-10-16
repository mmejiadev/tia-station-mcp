using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A field that shows a tag's value, and lets the operator type one when it is an input.</summary>
    public sealed class HmiIoField : HmiScreenItem
    {
        /// <summary>Creates an I/O field.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="tag">The HMI tag it shows.</param>
        /// <param name="mode"><c>Output</c>, <c>Input</c> or <c>InOutput</c>, as TIA Portal spells them.</param>
        /// <param name="formatPattern">The digits shown, for example <c>9999</c> for four.</param>
        public HmiIoField(HmiItemFrame frame, string tag, string mode, string formatPattern)
            : base(frame)
        {
            Tag = tag;
            Mode = mode;
            FormatPattern = formatPattern;
        }

        /// <summary>The HMI tag it shows.</summary>
        public string Tag { get; }

        /// <summary><c>Output</c>, <c>Input</c> or <c>InOutput</c>.</summary>
        public string Mode { get; }

        /// <summary>The digits shown, for example <c>9999</c>.</summary>
        public string FormatPattern { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var style = HmiItemStyle.IoField();
            style["Mode"] = Mode;
            style["FormatPattern"] = FormatPattern;
            style["FieldLength"] = FormatPattern.Length.ToString(CultureInfo.InvariantCulture);

            var placed = HmiScreenWriter.Place(style, Frame, writer.NextTabIndex());

            return writer.Item("Hmi.Screen.IOField", placed, () => new[] { writer.Font(ResolveFont()), writer.EmptyText("HelpText"), writer.ProcessValue(Tag) }
                .Concat(WriteAnimations(writer))
                .ToList());
        }
    }
}
