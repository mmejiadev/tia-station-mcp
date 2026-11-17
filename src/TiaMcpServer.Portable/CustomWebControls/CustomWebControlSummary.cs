using System.Collections.Generic;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>What a Custom Web Control's manifest says about it: enough to place it and give its interface values.</summary>
    public sealed class CustomWebControlSummary
    {
        /// <summary>Creates the summary.</summary>
        /// <param name="names">Its <c>identity.name</c>, which screens place it by, and what TIA Portal's toolbox shows.</param>
        /// <param name="properties">Each property's name and kind; the kind is null for a type this server does not set, such as an object.</param>
        /// <param name="events">The names of the events it fires.</param>
        /// <param name="size">Its default size from the manifest, in pixels; zero when the manifest gives none.</param>
        public CustomWebControlSummary((string Name, string DisplayName) names, IReadOnlyList<(string Name, CustomWebControlPropertyKind? Kind)> properties, IReadOnlyList<string> events, (int Width, int Height) size)
        {
            Name = names.Name;
            DisplayName = names.DisplayName;
            Properties = properties;
            Events = events;
            DefaultWidth = size.Width;
            DefaultHeight = size.Height;
        }

        /// <summary>Its <c>identity.name</c>.</summary>
        public string Name { get; }

        /// <summary>What TIA Portal's toolbox shows.</summary>
        public string DisplayName { get; }

        /// <summary>Each property's name and kind; null for a kind this server does not set.</summary>
        public IReadOnlyList<(string Name, CustomWebControlPropertyKind? Kind)> Properties { get; }

        /// <summary>The names of the events it fires.</summary>
        public IReadOnlyList<string> Events { get; }

        /// <summary>The default width from the manifest; zero when it gives none.</summary>
        public int DefaultWidth { get; }

        /// <summary>The default height from the manifest; zero when it gives none.</summary>
        public int DefaultHeight { get; }
    }
}
