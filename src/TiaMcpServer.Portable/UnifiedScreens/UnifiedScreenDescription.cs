using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A WinCC Unified screen to draw: its own properties and its items.</summary>
    public sealed class UnifiedScreenDescription
    {
        /// <summary>A symbolic IO field's property naming the text list it shows.</summary>
        public const string ResourceListProperty = "ResourceList";

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

        private static bool IsResourceList(UnifiedPropertySetting setting)
        {
            return setting.Path.Equals(ResourceListProperty, StringComparison.OrdinalIgnoreCase)
                && setting.Value.ValueKind == System.Text.Json.JsonValueKind.String
                && setting.Value.GetString()!.Trim().Length > 0;
        }

        /// <summary>Whether the runtime opens with this screen.</summary>
        public bool IsStartScreen { get; }

        /// <summary>The screen's own properties.</summary>
        public IReadOnlyList<UnifiedPropertySetting> Properties { get; }

        /// <summary>The items.</summary>
        public IReadOnlyList<UnifiedItemDescription> Items { get; }

        /// <summary>Every text list the items show: through a list animation, or a symbolic IO field's <c>ResourceList</c>.</summary>
        /// <remarks>Openness takes the name of a list that does not exist on either without a word (measured 2026-10-25).</remarks>
        public IReadOnlyList<string> LinkedTextLists => Items
            .SelectMany(item => item.Animations.OfType<UnifiedListAnimation>().Select(animation => animation.List)
                .Concat(item.Properties.Where(IsResourceList).Select(setting => setting.Value.GetString()!)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>Every HMI tag the items link to or animate by, once each.</summary>
        public IReadOnlyList<string> LinkedTags => Items
            .SelectMany(item => item.Tags.Values.Concat(item.Animations.SelectMany(animation => animation.LinkedTags)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
