namespace TiaMcpServer.Siemens
{
    /// <summary>One row of a PLC tag table an edit adds or changes: a tag's name, and what of it is given.</summary>
    /// <remarks>A row added gives a data type and an address; a row changed gives only what changes, null for the rest.</remarks>
    public sealed class TagTableRow
    {
        /// <summary>Creates a row.</summary>
        /// <param name="name">The tag's name.</param>
        /// <param name="dataTypeName">Its data type, or null to leave it.</param>
        /// <param name="address">Its logical address, e.g. <c>%I0.0</c>, or null to leave it.</param>
        /// <param name="comment">Its comment in the editing language, or null to leave it.</param>
        public TagTableRow(string name, string? dataTypeName, string? address, string? comment)
        {
            Name = name;
            DataTypeName = dataTypeName;
            Address = address;
            Comment = comment;
        }

        /// <summary>The tag's name.</summary>
        public string Name { get; }

        /// <summary>Its data type, or null to leave it.</summary>
        public string? DataTypeName { get; }

        /// <summary>Its logical address, or null to leave it.</summary>
        public string? Address { get; }

        /// <summary>Its comment, or null to leave it.</summary>
        public string? Comment { get; }
    }
}
