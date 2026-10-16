namespace TiaMcpServer.Hmi
{
    /// <summary>One range of an appearance animation: the values it covers and how the item looks then.</summary>
    public sealed class HmiRange
    {
        /// <summary>The text colour a range takes when it names none: the one the class ranges use.</summary>
        public const string DefaultForeColor = "24, 28, 49";

        /// <summary>Creates a range.</summary>
        /// <param name="from">The lowest value it covers.</param>
        /// <param name="to">The highest value it covers; equal to <paramref name="from"/> for a single value.</param>
        /// <param name="look">The background and text colours in the range; empty colours keep the item's.</param>
        /// <param name="isFlashing">Whether the item flashes while the value is in the range.</param>
        public HmiRange(double from, double to, HmiLook look, bool isFlashing)
        {
            From = from;
            To = to;
            Look = look;
            IsFlashing = isFlashing;
        }

        /// <summary>The lowest value it covers.</summary>
        public double From { get; }

        /// <summary>The highest value it covers.</summary>
        public double To { get; }

        /// <summary>The background and text colours in the range.</summary>
        public HmiLook Look { get; }

        /// <summary>Whether the item flashes while the value is in the range.</summary>
        public bool IsFlashing { get; }
    }
}
