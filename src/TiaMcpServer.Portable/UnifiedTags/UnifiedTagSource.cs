namespace TiaMcpServer.UnifiedTags
{
    /// <summary>Where a WinCC Unified tag's value comes from: the HMI itself, with a data type, or a PLC tag through a connection.</summary>
    public sealed class UnifiedTagSource
    {
        private UnifiedTagSource(string? dataType, UnifiedTagLink? link)
        {
            DataType = dataType;
            Link = link;
        }

        /// <summary>An internal tag's data type, as <see cref="UnifiedTagDataTypes"/> spells it; null for a linked tag.</summary>
        public string? DataType { get; }

        /// <summary>The connection and PLC tag of a linked tag; null for an internal one.</summary>
        public UnifiedTagLink? Link { get; }

        /// <summary>Whether the tag is linked to a PLC tag.</summary>
        public bool IsLinked => Link != null;

        /// <summary>An internal tag.</summary>
        /// <param name="dataType">Its data type, already normalised.</param>
        /// <returns>The source.</returns>
        public static UnifiedTagSource Internal(string dataType)
        {
            return new UnifiedTagSource(dataType, null);
        }

        /// <summary>A tag linked to a PLC tag, whose data type TIA Portal takes from the PLC tag.</summary>
        /// <param name="link">The connection and PLC tag.</param>
        /// <returns>The source.</returns>
        public static UnifiedTagSource Linked(UnifiedTagLink link)
        {
            return new UnifiedTagSource(null, link);
        }
    }
}
