namespace TiaMcpServer.UnifiedTags
{
    /// <summary>How a WinCC Unified HMI tag reaches the PLC: the connection, and the PLC tag by name.</summary>
    public sealed class UnifiedTagLink
    {
        /// <summary>Creates a link.</summary>
        /// <param name="connection">An existing integrated connection of the HMI, e.g. <c>HMI_Connection_1</c>.</param>
        /// <param name="plcTag">The PLC tag's name, as in the PLC's tag table.</param>
        public UnifiedTagLink(string connection, string plcTag)
        {
            Connection = connection;
            PlcTag = plcTag;
        }

        /// <summary>The connection the tag is read through.</summary>
        public string Connection { get; }

        /// <summary>The PLC tag's name.</summary>
        public string PlcTag { get; }
    }
}
