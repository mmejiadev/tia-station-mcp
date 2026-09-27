using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The project's network layout.</summary>
    public sealed class ResponseNetworkTopology : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="nodes">One line per interface: device, interface, type, address, subnet.</param>
        public ResponseNetworkTopology(IReadOnlyList<string> nodes)
        {
            Nodes = nodes;
        }

        /// <summary>
        /// One line per interface, as <c>device | interface | type | address | subnet | profinet name</c>. An empty
        /// subnet means the interface is wired to nothing, which is a common and otherwise silent
        /// reason a download or an IO connection fails.
        /// </summary>
        public IReadOnlyList<string> Nodes { get; }
    }
}
