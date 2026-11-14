namespace TiaMcpServer.WatchTables
{
    /// <summary>
    /// One row to write into a watch or force table: an address, how it is shown, and what is prepared for it.
    /// </summary>
    /// <remarks>Built by <see cref="WatchTableRowReader"/>, which has already checked every field.</remarks>
    public sealed class WatchTableRow
    {
        /// <summary>Creates a checked row.</summary>
        /// <param name="address">The absolute address, e.g. <c>%Q0.0</c>.</param>
        /// <param name="displayFormat">How the value is shown, as Openness names it, e.g. <c>Bool</c>.</param>
        /// <param name="value">The value prepared to modify or force it with, or empty to only watch it.</param>
        /// <param name="comment">The row's comment, or empty.</param>
        public WatchTableRow(string address, string displayFormat, string value, string comment)
        {
            Address = address;
            DisplayFormat = displayFormat;
            Value = value;
            Comment = comment;
        }

        /// <summary>The absolute address.</summary>
        public string Address { get; }

        /// <summary>How the value is shown.</summary>
        public string DisplayFormat { get; }

        /// <summary>The value prepared for it, or empty.</summary>
        public string Value { get; }

        /// <summary>The row's comment, or empty.</summary>
        public string Comment { get; }
    }
}
