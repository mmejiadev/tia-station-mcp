using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Properties to set on an item already on a screen, leaving everything else of it as it is.</summary>
    public sealed class UnifiedItemSettings
    {
        /// <summary>Creates the settings of one item.</summary>
        /// <param name="name">The item's name on the screen.</param>
        /// <param name="properties">The properties to set, at least one.</param>
        public UnifiedItemSettings(string name, IReadOnlyList<UnifiedPropertySetting> properties)
        {
            Name = name;
            Properties = properties;
        }

        /// <summary>The item's name on the screen.</summary>
        public string Name { get; }

        /// <summary>The properties to set.</summary>
        public IReadOnlyList<UnifiedPropertySetting> Properties { get; }
    }
}
