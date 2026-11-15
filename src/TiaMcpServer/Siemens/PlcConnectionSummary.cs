namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A connection of a PLC as TIA Portal holds it, seen from that PLC: its kind, name, validity and the two ends.
    /// </summary>
    public sealed class PlcConnectionSummary
    {
        /// <summary>Creates the description of a connection.</summary>
        /// <param name="kind">TIA Portal's kind, e.g. <c>S7Connection</c>.</param>
        /// <param name="name">The name TIA Portal gave it, e.g. <c>S7_Connection_1</c>; empty for a kind without one.</param>
        /// <param name="isValid">Whether TIA Portal reports it valid.</param>
        /// <param name="ends">Its two ends.</param>
        public PlcConnectionSummary(string kind, string name, bool isValid, PlcConnectionEnds ends)
        {
            Kind = kind;
            Name = name;
            IsValid = isValid;
            Ends = ends;
        }

        /// <summary>TIA Portal's kind, e.g. <c>S7Connection</c> or <c>TcpConnection</c>.</summary>
        public string Kind { get; }

        /// <summary>The name TIA Portal gave it; empty for a kind without one.</summary>
        public string Name { get; }

        /// <summary>Whether TIA Portal reports it valid.</summary>
        public bool IsValid { get; }

        /// <summary>Its two ends, this PLC's first.</summary>
        public PlcConnectionEnds Ends { get; }
    }
}
