using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>
    /// A field that shows a text chosen by a tag's value — "STOPPED", "AUTOMATIC" — and, as an input,
    /// lets the operator pick one from a drop-down list.
    /// </summary>
    /// <remarks>
    /// The texts are a text list on the panel, linked by name; GenerateHmiTextList writes one.
    /// Children in the order the class export lists them: events, font, tooltip, process value, then
    /// the <c>TextOff</c> and <c>TextOn</c> texts it carried, <c>0</c> and <c>1</c>. The class field
    /// opens the screen whose number the operator picks, on its <c>Change</c> event.
    /// </remarks>
    public sealed class HmiSymbolicIoField : HmiScreenItem
    {
        /// <summary>Creates a symbolic I/O field.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="source">The tag and the text list.</param>
        /// <param name="mode"><c>Output</c>, <c>Input</c> or <c>InOutput</c>.</param>
        /// <param name="events">What its events do; possibly none.</param>
        public HmiSymbolicIoField(HmiItemFrame frame, HmiSymbolicSource source, string mode, IReadOnlyList<HmiEventBinding> events)
            : base(frame)
        {
            Source = source;
            Mode = mode;
            Events = events;
        }

        /// <summary>The tag and the text list.</summary>
        public HmiSymbolicSource Source { get; }

        /// <summary><c>Output</c>, <c>Input</c> or <c>InOutput</c>.</summary>
        public string Mode { get; }

        /// <summary>What its events do.</summary>
        public IReadOnlyList<HmiEventBinding> Events { get; }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var style = HmiItemStyle.SymbolicIoField();
            style["Mode"] = Mode;

            var placed = HmiScreenWriter.Place(style, Frame, writer.NextTabIndex());

            return writer.Node("Hmi.Screen.SymbolicIOField", "ScreenItems", () => new XElement?[]
            {
                HmiScreenWriter.Attributes(placed),
                HmiScreenWriter.Link("TextList", Source.TextList),
                HmiScreenWriter.Objects(Events.Select(binding => binding.Write(writer))
                    .Concat(new[]
                    {
                        writer.Font(ResolveFont()),
                        writer.EmptyText("HelpText"),
                        writer.ProcessValue(Source.Tag),
                        writer.Text("TextOff", "0"),
                        writer.Text("TextOn", "1")
                    })
                    .Concat(WriteAnimations(writer))
                    .ToList())
            });
        }
    }
}
