using System.Collections.Generic;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>An item shown or hidden by one bit of a tag: an alarm light that appears only on alarm.</summary>
    public sealed class HmiVisibilityAnimation : HmiAnimation
    {
        /// <summary>Creates the animation.</summary>
        /// <param name="tag">The HMI tag.</param>
        /// <param name="bit">The bit of the tag, 0 for a Bool.</param>
        /// <param name="isVisibleWhenSet">True to show the item while the bit is 1; false to hide it then.</param>
        public HmiVisibilityAnimation(string tag, int bit, bool isVisibleWhenSet)
        {
            Tag = tag;
            Bit = bit;
            IsVisibleWhenSet = isVisibleWhenSet;
        }

        /// <summary>The kind as a description names it.</summary>
        public const string KindName = "visibility";

        /// <inheritdoc />
        public override string Kind => KindName;

        /// <summary>The HMI tag.</summary>
        public string Tag { get; }

        /// <summary>The bit of the tag.</summary>
        public int Bit { get; }

        /// <summary>True to show the item while the bit is 1; false to hide it then.</summary>
        public bool IsVisibleWhenSet { get; }

        internal override XElement Write(HmiScreenWriter writer)
        {
            return writer.Node("Hmi.Dynamic.SingleBitVisibilityAnimation", "Animations", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string>
                {
                    ["BitPosition"] = HmiScreenWriter.Number(Bit),
                    ["Name"] = "VisibilityAnimation",
                    ["Visible"] = IsVisibleWhenSet ? "true" : "false"
                }),
                HmiScreenWriter.Objects(new[] { writer.TagTrigger("VisibilityTag", Tag) })
            });
        }
    }
}
