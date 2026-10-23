namespace TiaMcpServer.UnifiedTags
{
    /// <summary>One WinCC Unified HMI tag to create: internal, with its data type, or linked to a PLC tag.</summary>
    public sealed class UnifiedTagDescription
    {
        /// <summary>Creates a description.</summary>
        /// <param name="name">The tag's name, unique on the HMI.</param>
        /// <param name="table">The tag table it goes in, or null for the HMI's default one.</param>
        /// <param name="dataType">An internal tag's data type, as <see cref="UnifiedTagDataTypes"/> spells it; null for a linked tag.</param>
        /// <param name="link">The connection and PLC tag of a linked tag; null for an internal one.</param>
        public UnifiedTagDescription(string name, string? table, string? dataType, UnifiedTagLink? link)
        {
            Name = name;
            Table = table;
            DataType = dataType;
            Link = link;
        }

        /// <summary>The tag's name.</summary>
        public string Name { get; }

        /// <summary>The tag table, or null for the default one.</summary>
        public string? Table { get; }

        /// <summary>An internal tag's data type; null for a linked tag, whose type TIA Portal takes from the PLC tag.</summary>
        public string? DataType { get; }

        /// <summary>The connection and PLC tag of a linked tag; null for an internal one.</summary>
        public UnifiedTagLink? Link { get; }
    }
}
