namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One PLC tag table: where it sits, and how much is in it.
    /// </summary>
    /// <remarks>
    /// The index a caller needs before asking for anything else. A program's tags are spread over
    /// several tables and nested groups, and the table a tag belongs in is part of its path, so
    /// this is what turns "add a tag" into an argument that can be written down.
    ///
    /// The default table is marked because it is the one TIA Portal writes into when nothing says
    /// otherwise, and a caller with no reason to choose should choose it.
    /// </remarks>
    public sealed class TagTableInfo
    {
        /// <summary>Creates a tag table description.</summary>
        /// <param name="path">Full path of the table, for example <c>Cell/IO</c>.</param>
        /// <param name="tagCount">How many tags it holds.</param>
        /// <param name="userConstantCount">How many user constants it holds.</param>
        /// <param name="isDefault">Whether this is the program's default table.</param>
        public TagTableInfo(string path, int tagCount, int userConstantCount, bool isDefault)
        {
            Path = path;
            TagCount = tagCount;
            UserConstantCount = userConstantCount;
            IsDefault = isDefault;
        }

        /// <summary>Full path of the table. This is what addresses it.</summary>
        public string Path { get; }

        /// <summary>How many tags it holds.</summary>
        public int TagCount { get; }

        /// <summary>How many user constants it holds.</summary>
        public int UserConstantCount { get; }

        /// <summary>
        /// The table TIA Portal writes into when nothing says otherwise. Exactly one table of a
        /// program is the default, and it is the sensible destination for a caller with no reason
        /// to prefer another.
        /// </summary>
        public bool IsDefault { get; }
    }
}
