using System.Collections.Generic;
using System.Text.Json;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>A colour that flashes between two values, tied to no tag.</summary>
    /// <remarks>
    /// Openness's flashing dynamization. It takes a colour property only: TIA Portal V20 refuses it on
    /// <c>Visible</c> ("Dynamization type not supported") and takes it on <c>BackColor</c> and
    /// <c>BorderColor</c> (measured 2026-10-25). A colour that flashes only while a tag is in a range is
    /// a <see cref="UnifiedRangeFlashing"/> on that range instead.
    /// </remarks>
    public sealed class UnifiedFlashingAnimation : UnifiedAnimation
    {
        /// <summary>The rates, as Openness V20 names them in <c>FlashingRate</c>.</summary>
        public static readonly IReadOnlyList<string> Rates = new[] { "Slow", "Medium", "Fast" };

        /// <summary>When it flashes, as Openness V20 names it in <c>FlashingCondition</c>.</summary>
        public static readonly IReadOnlyList<string> Conditions = new[] { "Always", "Never", "RangeViolation" };

        /// <summary>Creates a flashing animation.</summary>
        /// <param name="path">The colour property's path.</param>
        /// <param name="colors">The two colours it shows in turn, as JSON.</param>
        /// <param name="rate">One of <see cref="Rates"/>.</param>
        /// <param name="condition">One of <see cref="Conditions"/>.</param>
        public UnifiedFlashingAnimation(string path, (JsonElement Color, JsonElement Alternate) colors, string rate, string condition)
            : base(path)
        {
            Color = colors.Color.Clone();
            Alternate = colors.Alternate.Clone();
            Rate = rate;
            Condition = condition;
        }

        /// <summary>The colour, as JSON.</summary>
        public JsonElement Color { get; }

        /// <summary>The colour shown in turn with it, as JSON.</summary>
        public JsonElement Alternate { get; }

        /// <summary>The rate.</summary>
        public string Rate { get; }

        /// <summary>When it flashes.</summary>
        public string Condition { get; }
    }
}
