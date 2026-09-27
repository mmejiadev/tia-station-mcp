namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One entry of a virtual controller's tag list.</summary>
    public sealed class ResponseSimulationTag
    {
        /// <summary>Creates the entry.</summary>
        /// <param name="name">The name a read or a write must use.</param>
        /// <param name="area">Input, Output, Marker, Timer, Counter or DataBlock.</param>
        /// <param name="dataType">The declared PLC data type.</param>
        /// <param name="isReadable">Whether this server can read a value for it.</param>
        public ResponseSimulationTag(string name, string area, string dataType, bool isReadable)
        {
            Name = name;
            Area = area;
            DataType = dataType;
            IsReadable = isReadable;
        }

        /// <summary>
        /// The name a read or a write must use, spelled exactly as the controller reports it. Members
        /// of a data block are fully qualified and carry no quotes: <c>DB_Cell.Feeder.Step</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>Input, Output, Marker, Timer, Counter or DataBlock.</summary>
        public string Area { get; }

        /// <summary>The declared PLC data type, e.g. Bool, Int, DInt, Real.</summary>
        public string DataType { get; }

        /// <summary>
        /// Whether this server can read a value for it. False for a struct or an array: read their
        /// members instead, which are separate entries in the same list.
        /// </summary>
        public bool IsReadable { get; }
    }
}
