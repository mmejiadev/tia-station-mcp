using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A text button that runs system functions on its events.</summary>
    public sealed class HmiButton : HmiScreenItem
    {
        private const string UnusedTextOn = "Text";

        /// <summary>Creates a button.</summary>
        /// <param name="frame">Its name, box, look and animations.</param>
        /// <param name="label">The text on it.</param>
        /// <param name="events">What its events do: pressing it, clicking it.</param>
        public HmiButton(HmiItemFrame frame, string label, IReadOnlyList<HmiEventBinding> events)
            : base(frame)
        {
            Label = label;
            Events = events;
        }

        /// <summary>The text on it.</summary>
        public string Label { get; }

        /// <summary>What its events do.</summary>
        public IReadOnlyList<HmiEventBinding> Events { get; }

        /// <inheritdoc />
        /// <remarks>
        /// Children in the order an export lists them: events, font, tooltip, label. The label is the
        /// <c>TextOff</c> text, which is the one a text button shows; <c>TextOn</c> is written with the
        /// placeholder TIA Portal gives every new button, which the class buttons keep.
        /// </remarks>
        internal override XElement Write(HmiScreenWriter writer)
        {
            var placed = HmiScreenWriter.Place(HmiItemStyle.Button(), Frame, writer.NextTabIndex());

            return writer.Item("Hmi.Screen.Button", placed, () => Events.Select(handler => handler.Write(writer))
                .Concat(new[]
                {
                    writer.Font(ResolveFont()),
                    writer.EmptyText("HelpText"),
                    writer.Text("TextOff", Label),
                    writer.Text("TextOn", UnusedTextOn)
                })
                .Concat(WriteAnimations(writer))
                .ToList());
        }
    }
}
