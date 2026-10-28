namespace TiaMcpServer.UnifiedTags
{
    /// <summary>One WinCC Unified HMI tag to create: internal, with its data type, or linked to a PLC tag, and its settings.</summary>
    public sealed class UnifiedTagDescription
    {
        /// <summary>Creates a description.</summary>
        /// <param name="name">The tag's name, unique on the HMI.</param>
        /// <param name="table">The tag table it goes in, or null for the HMI's default one.</param>
        /// <param name="source">Its data type, or the PLC tag it is linked to.</param>
        /// <param name="settings">Comment, start value, limits and the rest; <see cref="UnifiedTagSettings.None"/> for none.</param>
        public UnifiedTagDescription(string name, string? table, UnifiedTagSource source, UnifiedTagSettings settings)
        {
            Name = name;
            Table = table;
            Source = source;
            Settings = settings;
        }

        /// <summary>The tag's name.</summary>
        public string Name { get; }

        /// <summary>The tag table, or null for the default one.</summary>
        public string? Table { get; }

        /// <summary>Its data type, or the PLC tag it is linked to.</summary>
        public UnifiedTagSource Source { get; }

        /// <summary>The settings written after the tag exists.</summary>
        public UnifiedTagSettings Settings { get; }

        /// <summary>An internal tag's data type; null for a linked tag, whose type TIA Portal takes from the PLC tag.</summary>
        public string? DataType => Source.DataType;

        /// <summary>The connection and PLC tag of a linked tag; null for an internal one.</summary>
        public UnifiedTagLink? Link => Source.Link;
    }
}
