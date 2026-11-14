namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A PLC's protection before and after it was left at full access.
    /// </summary>
    public sealed class PlcAccessChange
    {
        /// <summary>Creates the record of one PLC's change.</summary>
        /// <param name="plcName">The PLC, as its device item is named.</param>
        /// <param name="before">Its protection before.</param>
        /// <param name="after">Its protection after, read back rather than assumed.</param>
        public PlcAccessChange(string plcName, PlcAccessState before, PlcAccessState after)
        {
            PlcName = plcName;
            Before = before;
            After = after;
        }

        /// <summary>The PLC, as its device item is named.</summary>
        public string PlcName { get; }

        /// <summary>Its protection before.</summary>
        public PlcAccessState Before { get; }

        /// <summary>Its protection after.</summary>
        public PlcAccessState After { get; }
    }
}
