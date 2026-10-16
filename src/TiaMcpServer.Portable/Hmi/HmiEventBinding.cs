using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>An event of an item and the system functions it runs, in order.</summary>
    public sealed class HmiEventBinding
    {
        /// <summary>Creates a handler.</summary>
        /// <param name="eventName">The event as TIA Portal names it: <c>Press</c>, <c>Click</c>, <c>Change</c>.</param>
        /// <param name="calls">The functions it runs, in order.</param>
        public HmiEventBinding(string eventName, IReadOnlyList<HmiFunctionCall> calls)
        {
            EventName = eventName;
            Calls = calls;
        }

        /// <summary>The event as TIA Portal names it.</summary>
        public string EventName { get; }

        /// <summary>The functions it runs, in order.</summary>
        public IReadOnlyList<HmiFunctionCall> Calls { get; }

        /// <summary>Writes the event with its function list.</summary>
        /// <param name="writer">The screen's writer.</param>
        /// <returns>The <c>Events</c> object.</returns>
        internal XElement Write(HmiScreenWriter writer)
        {
            return writer.Node("Hmi.Event.Event", "Events", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string> { ["Name"] = EventName }),
                HmiScreenWriter.Objects(new[]
                {
                    writer.Node("Hmi.Event.FunctionListEventHandler", "EventHandler", () => new[]
                    {
                        HmiScreenWriter.Objects(Calls.Select(call => call.WriteEntry(writer)).ToList())
                    })
                })
            });
        }
    }
}
