using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>A group: items that move and resize as one, such as a tank drawn from several pictures.</summary>
    /// <remarks>
    /// As the class exports write one (<c>0967-HMI_PROCÉS</c>, 21 groups): the group carries only its
    /// name and a tab index of -1, and its items are its own screen items, each with its own box. A group
    /// has no box of its own in the export; TIA Portal works it out from the items.
    /// </remarks>
    public sealed class HmiGroup : HmiScreenItem
    {
        /// <summary>Creates a group.</summary>
        /// <param name="frame">Its name; the box, look and animations are not written.</param>
        /// <param name="items">Its items, at least two.</param>
        public HmiGroup(HmiItemFrame frame, IReadOnlyList<HmiScreenItem> items)
            : base(frame)
        {
            Items = items;
        }

        /// <summary>Its items.</summary>
        public IReadOnlyList<HmiScreenItem> Items { get; }

        /// <summary>An item and, for a group, every item inside it, however deep.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The item first, then what it contains.</returns>
        public static IEnumerable<HmiScreenItem> Flatten(HmiScreenItem item)
        {
            var contained = item is HmiGroup group ? group.Items.SelectMany(Flatten) : Enumerable.Empty<HmiScreenItem>();

            return new[] { item }.Concat(contained);
        }

        /// <inheritdoc />
        internal override XElement Write(HmiScreenWriter writer)
        {
            var attributes = new Dictionary<string, string>
            {
                ["ObjectName"] = Frame.Name,
                ["TabIndex"] = HmiScreenWriter.Number(HmiScreenWriter.NotFocusable)
            };

            return writer.Item("Hmi.Screen.Group", attributes, () => Items.Select(item => item.Write(writer)).ToList());
        }
    }
}
