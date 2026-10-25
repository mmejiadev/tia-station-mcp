using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A property that takes a value for each range of a tag: the class's pilot light, green when running.</summary>
    /// <remarks>
    /// Openness's tag dynamization with a range mapping table. The ranges are checked here because TIA
    /// Portal is not: it takes a range whose start is past its end, and ranges that overlap, without a
    /// word (measured 2026-10-25 on TIA Portal V20).
    /// </remarks>
    public sealed class UnifiedRangeAnimation : UnifiedAnimation
    {
        /// <summary>Creates a range animation.</summary>
        /// <param name="path">The property's path.</param>
        /// <param name="tag">The HMI tag whose value picks the range.</param>
        /// <param name="ranges">The ranges, in order, none overlapping.</param>
        public UnifiedRangeAnimation(string path, string tag, IReadOnlyList<UnifiedValueRange> ranges)
            : base(path)
        {
            Tag = tag;
            Ranges = ranges;
        }

        /// <summary>The HMI tag.</summary>
        public string Tag { get; }

        /// <summary>The ranges.</summary>
        public IReadOnlyList<UnifiedValueRange> Ranges { get; }

        /// <inheritdoc />
        public override IReadOnlyList<string> LinkedTags => new[] { Tag };
    }
}
