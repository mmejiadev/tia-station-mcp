using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The children of one node on an OPC UA server.</summary>
    public sealed class ResponseOpcUaNodes : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="nodes">One line per child.</param>
        public ResponseOpcUaNodes(IReadOnlyList<string> nodes)
        {
            Nodes = nodes;
        }

        /// <summary>
        /// One line per child, as <c>node id | display name | node class</c>. The node id is what
        /// a read or a further browse takes, copied exactly: the quotes around Siemens names are
        /// part of it.
        /// </summary>
        public IReadOnlyList<string> Nodes { get; }
    }
}
