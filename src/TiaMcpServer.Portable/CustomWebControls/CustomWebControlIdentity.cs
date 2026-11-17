using System;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Who a Custom Web Control is: the name TIA Portal places it by, the name it shows, and its GUID.</summary>
    public sealed class CustomWebControlIdentity
    {
        /// <summary>Creates the identity.</summary>
        /// <param name="name">The manifest's <c>identity.name</c>, e.g. <c>TiaStation.PilotLight</c>; screens place the control by it.</param>
        /// <param name="displayName">What TIA Portal's toolbox shows.</param>
        /// <param name="description">What the control is for.</param>
        public CustomWebControlIdentity(string name, string displayName, string description)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            PackageId = CustomWebControlGuid.For(name);
        }

        /// <summary>The manifest's <c>identity.name</c>.</summary>
        public string Name { get; }

        /// <summary>What TIA Portal's toolbox shows.</summary>
        public string DisplayName { get; }

        /// <summary>What the control is for.</summary>
        public string Description { get; }

        /// <summary>Its GUID, which names its package: the same name always gives the same one, so installing again replaces it.</summary>
        public Guid PackageId { get; }
    }
}
