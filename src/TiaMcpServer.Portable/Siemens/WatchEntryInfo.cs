namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One row of a watch or force table: an address, how it is displayed, and what is prepared
    /// for it.
    /// </summary>
    /// <remarks>
    /// A row carries no value read from a machine, because there is none to carry: Openness
    /// creates these tables and fills them in, and never reads what a CPU holds. What a row says
    /// is which address somebody will be looking at, and what they have queued up to do to it.
    /// </remarks>
    public sealed class WatchEntryInfo
    {
        /// <summary>Creates a row description.</summary>
        /// <param name="address">The address or symbol the row watches, for example <c>%Q0.0</c>.</param>
        /// <param name="displayFormat">How the value is shown: <c>Bool</c>, <c>Hex</c>, <c>DEC_signed</c>.</param>
        /// <param name="monitorTrigger">When the value is read, for example <c>Permanent</c>.</param>
        /// <param name="intention">What is prepared for the address beyond watching it.</param>
        public WatchEntryInfo(string address, string displayFormat, string monitorTrigger, WatchEntryIntention intention)
        {
            Address = address;
            DisplayFormat = displayFormat;
            MonitorTrigger = monitorTrigger;
            Intention = intention;
        }

        /// <summary>The address or symbol the row watches.</summary>
        public string Address { get; }

        /// <summary>How the value is shown.</summary>
        public string DisplayFormat { get; }

        /// <summary>When the value is read.</summary>
        public string MonitorTrigger { get; }

        /// <summary>What is prepared for the address beyond watching it.</summary>
        public WatchEntryIntention Intention { get; }

        /// <summary>The row as one line.</summary>
        public string Line => $"{Address} | {DisplayFormat} | {MonitorTrigger} | {Intention.Line}";
    }
}
