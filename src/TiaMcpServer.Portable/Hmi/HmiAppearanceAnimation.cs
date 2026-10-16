using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>An item's colours following a tag's value through ranges: green below 50, red above.</summary>
    /// <remarks>
    /// The <c>RangeAppearanceAnimation</c> of the class pilot lights, which have two ranges, 0 and 1.
    /// The shape takes any number. Each range writes both colours, because the class exports always
    /// carry both; a range that names no background keeps the item's own, and no text colour the
    /// class one.
    /// </remarks>
    public sealed class HmiAppearanceAnimation : HmiAnimation
    {
        private const string FlashingOn = "Medium";
        private const string FlashingOff = "No";

        /// <summary>Creates the animation.</summary>
        /// <param name="tag">The HMI tag whose value selects the range.</param>
        /// <param name="ranges">The ranges, in order.</param>
        /// <param name="itemBackColor">The item's own background, for a range that names none.</param>
        public HmiAppearanceAnimation(string tag, IReadOnlyList<HmiRange> ranges, string itemBackColor)
        {
            Tag = tag;
            Ranges = ranges;
            ItemBackColor = itemBackColor;
        }

        /// <summary>The kind as a description names it.</summary>
        public const string KindName = "appearance";

        /// <inheritdoc />
        public override string Kind => KindName;

        /// <summary>The HMI tag whose value selects the range.</summary>
        public string Tag { get; }

        /// <summary>The ranges, in order.</summary>
        public IReadOnlyList<HmiRange> Ranges { get; }

        /// <summary>The item's own background, for a range that names none.</summary>
        public string ItemBackColor { get; }

        internal override XElement Write(HmiScreenWriter writer)
        {
            return writer.Node("Hmi.Dynamic.RangeAppearanceAnimation", "Animations", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string> { ["Name"] = "RangeAppearanceAnimation" }),
                HmiScreenWriter.Objects(new[] { writer.TagTrigger("RangeTag", Tag) }.Concat(Ranges.Select(range => WriteRange(writer, range))).ToList())
            });
        }

        private XElement WriteRange(HmiScreenWriter writer, HmiRange range)
        {
            return writer.Node("Hmi.Dynamic.Range", "RangeValues", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string>
                {
                    ["BackColor"] = range.Look.BackColor.Length > 0 ? range.Look.BackColor : ItemBackColor,
                    ["FlashingType"] = range.IsFlashing ? FlashingOn : FlashingOff,
                    ["ForeColor"] = range.Look.ForeColor.Length > 0 ? range.Look.ForeColor : HmiRange.DefaultForeColor,
                    ["LowerLimit"] = HmiScreenWriter.Number(range.From),
                    ["UpperLimit"] = HmiScreenWriter.Number(range.To)
                })
            });
        }
    }
}
