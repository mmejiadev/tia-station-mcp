using System.Text.Json;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>How a colour flashes while its tag is in one range: the colour it alternates with, and how fast.</summary>
    public sealed class UnifiedRangeFlashing
    {
        /// <summary>Creates the flashing of a range.</summary>
        /// <param name="rate">The rate, one of <see cref="UnifiedFlashingAnimation.Rates"/>.</param>
        /// <param name="alternate">The colour shown in turn with the range's own, as JSON.</param>
        public UnifiedRangeFlashing(string rate, JsonElement alternate)
        {
            Rate = rate;
            Alternate = alternate.Clone();
        }

        /// <summary>The rate.</summary>
        public string Rate { get; }

        /// <summary>The alternate colour, as JSON.</summary>
        public JsonElement Alternate { get; }
    }
}
