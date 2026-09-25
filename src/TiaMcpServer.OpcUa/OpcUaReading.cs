namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// One value read from a running controller, detached from the OPC UA stack.
    /// </summary>
    /// <remarks>
    /// A bad status is a reading too, not an exception. Asked for ten variables where one is
    /// misspelt, the caller gets nine values and one line saying which node the server did not
    /// know. Failing the whole read over one name would hide the nine.
    /// </remarks>
    public sealed class OpcUaReading
    {
        /// <summary>Creates a reading.</summary>
        /// <param name="nodeId">The node as the caller named it.</param>
        /// <param name="value">The value as invariant text, or empty when the status is bad.</param>
        /// <param name="dataType">The OPC UA built-in type of the value.</param>
        /// <param name="status">The server's status for this value, for example <c>Good</c>.</param>
        /// <param name="isGood">Whether the status says the value can be trusted.</param>
        /// <param name="sourceTimestamp">When the controller produced it, ISO 8601 UTC, or empty.</param>
        public OpcUaReading(string nodeId, string value, string dataType, string status, bool isGood, string sourceTimestamp)
        {
            NodeId = nodeId;
            Value = value;
            DataType = dataType;
            Status = status;
            IsGood = isGood;
            SourceTimestamp = sourceTimestamp;
        }

        /// <summary>The node as the caller named it.</summary>
        public string NodeId { get; }

        /// <summary>
        /// The value as text, formatted with the invariant culture so that a half reads as
        /// <c>0.5</c> on a Spanish Windows as well. Arrays are written as <c>[a, b, c]</c>.
        /// </summary>
        public string Value { get; }

        /// <summary>The OPC UA built-in type, for example <c>Boolean</c> or <c>Int16</c>.</summary>
        public string DataType { get; }

        /// <summary>The server's status for this value.</summary>
        public string Status { get; }

        /// <summary>Whether the status says the value can be trusted.</summary>
        public bool IsGood { get; }

        /// <summary>When the controller produced the value, ISO 8601 UTC, or empty.</summary>
        public string SourceTimestamp { get; }
    }
}
