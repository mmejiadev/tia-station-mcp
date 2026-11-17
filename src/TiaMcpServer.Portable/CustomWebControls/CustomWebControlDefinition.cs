using System.Collections.Generic;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>A whole Custom Web Control, ready to be packaged: who it is, what it offers, and what it looks like.</summary>
    /// <remarks>Built only by <see cref="CustomWebControlReader"/>, which checks every part first.</remarks>
    public sealed class CustomWebControlDefinition
    {
        /// <summary>Creates the definition.</summary>
        /// <param name="identity">Who it is.</param>
        /// <param name="properties">The properties its interface offers, in order.</param>
        /// <param name="events">The events it fires.</param>
        /// <param name="content">What it looks like and does.</param>
        public CustomWebControlDefinition(CustomWebControlIdentity identity, IReadOnlyList<CustomWebControlProperty> properties, IReadOnlyList<CustomWebControlEvent> events, CustomWebControlContent content)
        {
            Identity = identity;
            Properties = properties;
            Events = events;
            Content = content;
        }

        /// <summary>Who it is.</summary>
        public CustomWebControlIdentity Identity { get; }

        /// <summary>The properties its interface offers.</summary>
        public IReadOnlyList<CustomWebControlProperty> Properties { get; }

        /// <summary>The events it fires.</summary>
        public IReadOnlyList<CustomWebControlEvent> Events { get; }

        /// <summary>What it looks like and does.</summary>
        public CustomWebControlContent Content { get; }
    }
}
