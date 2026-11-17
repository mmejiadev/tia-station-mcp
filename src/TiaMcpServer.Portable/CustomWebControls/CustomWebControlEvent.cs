using System.Collections.Generic;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>An event a Custom Web Control fires, which a screen gives a script.</summary>
    public sealed class CustomWebControlEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="name">Its name, as TIA Portal lists it under the control's events, e.g. <c>Clicked</c>.</param>
        /// <param name="arguments">What it passes, in order; their defaults are not used.</param>
        public CustomWebControlEvent(string name, IReadOnlyList<CustomWebControlProperty> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <summary>Its name.</summary>
        public string Name { get; }

        /// <summary>What it passes, in order.</summary>
        public IReadOnlyList<CustomWebControlProperty> Arguments { get; }
    }
}
