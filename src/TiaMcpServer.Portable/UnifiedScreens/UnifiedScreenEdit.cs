using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A change to some items of a WinCC Unified screen, leaving the others as they are.</summary>
    /// <remarks>
    /// Applied in this order: the screen's own properties, then <see cref="Remove"/>, <see cref="Set"/> and <see cref="Draw"/>.
    /// An item drawn under the name of one already there replaces it, and like every item drawn comes on top of the others.
    /// </remarks>
    public sealed class UnifiedScreenEdit
    {
        /// <summary>Creates an edit.</summary>
        /// <param name="screen">The screen's name.</param>
        /// <param name="properties">The screen's own properties to set.</param>
        /// <param name="items">What happens to the items: removed by name, properties set, drawn.</param>
        public UnifiedScreenEdit(string screen, IReadOnlyList<UnifiedPropertySetting> properties, UnifiedItemChanges items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            Screen = screen;
            Properties = properties;
            Remove = items.Remove;
            Set = items.Set;
            Draw = items.Draw;
            Drawing = new UnifiedScreenDescription(screen, false, properties, items.Draw);
        }

        /// <summary>The screen's name.</summary>
        public string Screen { get; }

        /// <summary>The screen's own properties to set.</summary>
        public IReadOnlyList<UnifiedPropertySetting> Properties { get; }

        /// <summary>The items to delete, by name.</summary>
        public IReadOnlyList<string> Remove { get; }

        /// <summary>The items whose properties are set, in place.</summary>
        public IReadOnlyList<UnifiedItemSettings> Set { get; }

        /// <summary>The items to draw: added, or replacing one of the same name.</summary>
        public IReadOnlyList<UnifiedItemDescription> Draw { get; }

        /// <summary>The items drawn, as a screen description, for the checks a drawing goes through.</summary>
        public UnifiedScreenDescription Drawing { get; }

        /// <summary>Every text list a property set in place names; those of the drawn items are in <see cref="Drawing"/>.</summary>
        public IReadOnlyList<string> SetTextLists => Set.SelectMany(item => item.Properties)
            .Where(UnifiedScreenDescription.IsResourceList)
            .Select(setting => setting.Value.GetString()!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>The names of the items already on the screen this edit changes or deletes.</summary>
        public IReadOnlyList<string> Touched => Remove.Concat(Set.Select(item => item.Name)).ToList();
    }
}
