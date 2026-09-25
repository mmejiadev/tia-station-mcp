namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// One node under a browsed parent, detached from the OPC UA stack.
    /// </summary>
    /// <remarks>
    /// The node identifier is the one thing a caller copies into a read, so it is written in the
    /// server's own notation (<c>ns=3;s="DB_Cell"."Running"</c>) with the namespace index resolved
    /// against this session. The quotes around Siemens names are part of the identifier, and
    /// "tidying" them away gives a string the server no longer recognises.
    /// </remarks>
    public sealed class OpcUaNode
    {
        /// <summary>Creates a node description.</summary>
        /// <param name="nodeId">The identifier to pass to a read or to browse further.</param>
        /// <param name="browseName">The name the server gives the node, with its namespace index.</param>
        /// <param name="displayName">The name a person would see.</param>
        /// <param name="nodeClass"><c>Object</c> for folders and structures, <c>Variable</c> for values.</param>
        public OpcUaNode(string nodeId, string browseName, string displayName, string nodeClass)
        {
            NodeId = nodeId;
            BrowseName = browseName;
            DisplayName = displayName;
            NodeClass = nodeClass;
        }

        /// <summary>The identifier to pass to a read or to browse further.</summary>
        public string NodeId { get; }

        /// <summary>The name the server gives the node.</summary>
        public string BrowseName { get; }

        /// <summary>The name a person would see.</summary>
        public string DisplayName { get; }

        /// <summary><c>Object</c> or <c>Variable</c>.</summary>
        public string NodeClass { get; }
    }
}
