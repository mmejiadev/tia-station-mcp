using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The configured connections of one PLC, one line each.</summary>
    public sealed class ResponsePlcConnections : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="connections">One line per connection.</param>
        public ResponsePlcConnections(IReadOnlyList<string> connections)
        {
            Connections = connections;
        }

        /// <summary>
        /// One line per connection, as
        /// <c>name | kind | valid | local device, address, ID, TSAP or port | partner device, address, ID, TSAP or port | who establishes it</c>.
        /// The local ID is the one the PLC's own instructions take: PUT, GET, BSEND and BRCV for S7, TSEND and TRCV for
        /// TCP and ISO-on-TCP.
        /// </summary>
        public IReadOnlyList<string> Connections { get; }
    }
}
