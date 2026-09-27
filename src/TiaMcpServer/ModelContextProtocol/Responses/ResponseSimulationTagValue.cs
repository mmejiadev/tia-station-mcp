namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The value of one tag of a virtual controller.</summary>
    public sealed class ResponseSimulationTagValue : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="name">The tag the value belongs to.</param>
        /// <param name="dataType">The declared PLC data type it was read as.</param>
        /// <param name="value">The value, or null when nothing was read.</param>
        public ResponseSimulationTagValue(string name, string dataType, object? value)
        {
            Name = name;
            DataType = dataType;
            Value = value;
        }

        /// <summary>The tag the value belongs to.</summary>
        public string Name { get; }

        /// <summary>The declared PLC data type it was read as.</summary>
        public string DataType { get; }

        /// <summary>
        /// The value, as a bool or a number rather than as text, so it can be compared without
        /// being parsed first — except a WChar, which is a one-character string. Null when nothing
        /// was read: a write the guard refused reports no value rather than a plausible one,
        /// because a refused Bool write reporting <c>false</c> would read as the tag holding false.
        /// </summary>
        public object? Value { get; }
    }
}
