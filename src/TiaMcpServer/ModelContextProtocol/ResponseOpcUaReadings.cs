using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Values read from a running controller over OPC UA.</summary>
    public sealed class ResponseOpcUaReadings : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="readings">One line per node asked for, in the order asked.</param>
        public ResponseOpcUaReadings(IReadOnlyList<string> readings)
        {
            Readings = readings;
        }

        /// <summary>
        /// One line per node, as <c>node id | value | type | status | source timestamp</c>. A node
        /// the server did not know has an empty value and a status that says why, and the other
        /// lines are still there.
        /// </summary>
        public IReadOnlyList<string> Readings { get; }
    }
}
