using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A WinCC Unified screen to draw: its own properties and its items.</summary>
    public sealed class UnifiedScreenDescription
    {
        /// <summary>Creates a screen description.</summary>
        /// <param name="name">The screen's name.</param>
        /// <param name="isStartScreen">Whether the runtime opens with it.</param>
        /// <param name="properties">The screen's own properties: <c>Width</c>, <c>BackColor</c>…</param>
        /// <param name="items">The items, drawn in this order, the last on top.</param>
        public UnifiedScreenDescription(string name, bool isStartScreen, IReadOnlyList<UnifiedPropertySetting> properties, IReadOnlyList<UnifiedItemDescription> items)
        {
            Name = name;
            IsStartScreen = isStartScreen;
            Properties = properties;
            Items = items;
        }

        /// <summary>The screen's name.</summary>
        public string Name { get; }

        /// <summary>Whether the runtime opens with this screen.</summary>
        public bool IsStartScreen { get; }

        /// <summary>The screen's own properties.</summary>
        public IReadOnlyList<UnifiedPropertySetting> Properties { get; }

        /// <summary>The items.</summary>
        public IReadOnlyList<UnifiedItemDescription> Items { get; }

        /// <summary>Every HMI tag the items link to or animate by, once each.</summary>
        public IReadOnlyList<string> LinkedTags => Items
            .SelectMany(item => item.Tags.Values.Concat(item.Animations.SelectMany(animation => animation.LinkedTags)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
