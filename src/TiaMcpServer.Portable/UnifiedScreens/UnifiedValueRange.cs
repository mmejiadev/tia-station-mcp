using System.Text.Json;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>One range of a <see cref="UnifiedRangeAnimation"/>: from, to, and the value the property takes.</summary>
    /// <remarks>
    /// The bounds are whole numbers whatever the tag's type: TIA Portal V20 takes a 16, 32 or 64-bit
    /// integer as a bound and refuses a floating-point one, even on a Real tag (measured 2026-10-25), and
    /// it refuses a missing bound, so a range is always closed. The value is kept as the JSON it came in,
    /// since its type is the animated property's and is only known against the Openness type.
    /// </remarks>
    public sealed class UnifiedValueRange
    {
        /// <summary>Creates a range.</summary>
        /// <param name="from">The first value in the range.</param>
        /// <param name="to">The last value in the range, at least <paramref name="from"/>.</param>
        /// <param name="value">The value the property takes, as JSON.</param>
        /// <param name="flashing">How the property flashes while the tag is in the range, or null when it does not.</param>
        public UnifiedValueRange(long from, long to, JsonElement value, UnifiedRangeFlashing? flashing)
        {
            From = from;
            To = to;
            Value = value.Clone();
            Flashing = flashing;
        }

        /// <summary>The first value in the range.</summary>
        public long From { get; }

        /// <summary>The last value in the range.</summary>
        public long To { get; }

        /// <summary>The value the property takes, as JSON.</summary>
        public JsonElement Value { get; }

        /// <summary>How the property flashes in the range, or null when it does not.</summary>
        public UnifiedRangeFlashing? Flashing { get; }
    }
}
