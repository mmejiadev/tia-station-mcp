using System.Collections.Generic;
using System.Text.Json;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>What a placed Custom Web Control's interface gets: fixed values, bound tags, event scripts.</summary>
    public sealed class CustomWebControlInterface
    {
        /// <summary>Creates the interface.</summary>
        /// <param name="values">Fixed values by property, as the JSON wrote them.</param>
        /// <param name="tags">The HMI tag bound to each property that follows one.</param>
        /// <param name="events">The script each event runs.</param>
        public CustomWebControlInterface(IReadOnlyDictionary<string, JsonElement> values, IReadOnlyDictionary<string, string> tags, IReadOnlyDictionary<string, string> events)
        {
            Values = values;
            Tags = tags;
            Events = events;
        }

        /// <summary>Fixed values by property.</summary>
        public IReadOnlyDictionary<string, JsonElement> Values { get; }

        /// <summary>The HMI tag bound to each property that follows one.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; }

        /// <summary>The script each event runs.</summary>
        public IReadOnlyDictionary<string, string> Events { get; }
    }
}
