namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One slot of a rack: its position, what it is labelled, and what is plugged into it.
    /// </summary>
    /// <remarks>
    /// The read that has to come before plugging anything. A slot number on its own is not enough
    /// to aim a module: racks number their slots differently, some positions are reserved for a
    /// power supply, and a module already plugged somewhere is the usual reason a plug is refused.
    /// Free and occupied slots are therefore one list rather than two, because the question being
    /// asked is "where does this go", and half an answer invites the wrong slot.
    /// </remarks>
    public sealed class PlugLocationInfo
    {
        /// <summary>Creates a slot description.</summary>
        /// <param name="positionNumber">The slot number, as Openness and TIA Portal count them.</param>
        /// <param name="label">What the rack calls the slot, or empty when it names it nothing.</param>
        /// <param name="occupantName">The module plugged there, or empty when the slot is free.</param>
        /// <param name="occupantTypeIdentifier">
        /// What the occupant is, as Openness names it — an order number. Empty when the slot is
        /// free, and the string to copy when plugging another one like it.
        /// </param>
        public PlugLocationInfo(
            int positionNumber,
            string label,
            string occupantName = "",
            string occupantTypeIdentifier = "")
        {
            PositionNumber = positionNumber;
            Label = label;
            OccupantName = occupantName;
            OccupantTypeIdentifier = occupantTypeIdentifier;
        }

        /// <summary>The slot number.</summary>
        public int PositionNumber { get; }

        /// <summary>What the rack calls the slot, or empty when it names it nothing.</summary>
        public string Label { get; }

        /// <summary>The module plugged there, or empty when the slot is free.</summary>
        public string OccupantName { get; }

        /// <summary>The occupant's order number, or empty when the slot is free.</summary>
        public string OccupantTypeIdentifier { get; }

        /// <summary>True when nothing is plugged into the slot.</summary>
        public bool IsFree => OccupantName.Length == 0;
    }
}
