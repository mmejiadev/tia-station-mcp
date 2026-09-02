namespace TiaMcpServer.Spec
{
    /// <summary>
    /// One piece moving from one station to the next.
    /// </summary>
    /// <remarks>
    /// A cell of N stations has N-1 handovers, and computing them here is what keeps every trace of
    /// logic out of the templates. A template with an "is this the last station?" conditional needs a
    /// template language, and a template language needs its own tests and its own debugger. With the
    /// pairs handed to it as a list, the last station having nowhere to hand over to is simply a
    /// shorter list.
    /// </remarks>
    public sealed class StationHandover
    {
        internal StationHandover(StationSpecification from, StationSpecification to, int fromIndex)
        {
            From = from;
            To = to;
            FromIndex = fromIndex;
        }

        /// <summary>The station letting the piece go.</summary>
        public StationSpecification From { get; }

        /// <summary>The station receiving it.</summary>
        public StationSpecification To { get; }

        /// <summary>One-based position of <see cref="From"/> in the cell.</summary>
        public int FromIndex { get; }

        /// <summary>One-based position of <see cref="To"/> in the cell.</summary>
        public int ToIndex => FromIndex + 1;
    }
}
