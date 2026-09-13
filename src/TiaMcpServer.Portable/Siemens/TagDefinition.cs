namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What to create in a tag table: a name inside a table, a data type, and what it is bound to.
    /// </summary>
    /// <remarks>
    /// A parameter object rather than four more arguments, and it earns its place: the table and
    /// the name arrive as one path, so the caller writes <c>Cell/IO/Start</c> the way it writes
    /// every other path in this server, and the split into "which table" and "which name" happens
    /// once, here, instead of at each call site.
    ///
    /// It is validated in the constructor because an entry with no table is the mistake worth
    /// catching early: a bare name would create a tag in whichever table the code happened to pick,
    /// which is exactly the ambiguity the full-path rule exists to prevent.
    /// </remarks>
    public sealed class TagDefinition
    {
        /// <summary>Reads a definition, checking that it names a table and an entry.</summary>
        /// <param name="path">Full path, for example <c>Cell/IO/Start</c>.</param>
        /// <param name="dataTypeName">The data type, for example <c>Bool</c>.</param>
        /// <param name="assignment">The logical address of a tag, or the value of a constant.</param>
        /// <exception cref="PortalException">
        /// The path names no table, or the data type or the assignment is missing.
        /// </exception>
        public TagDefinition(string path, string dataTypeName, string assignment)
        {
            var parsed = ProjectPath.Parse(path);

            if (parsed.IsTopLevel)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{path}' names an entry but no tag table. Write it as 'TagTable/Name', or " +
                    "'Group/TagTable/Name' when the table sits in a group; call GetTagTables to see them.");
            }

            if (string.IsNullOrWhiteSpace(dataTypeName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "dataType is required, for example 'Bool' or 'Int'");
            }

            if (string.IsNullOrWhiteSpace(assignment))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "An address or a value is required: a tag needs one like '%I0.0' and a constant needs one like '16'");
            }

            Path = parsed.ToString();
            TablePath = parsed.Parent;
            Name = parsed.Name;
            DataTypeName = dataTypeName.Trim();
            Assignment = assignment.Trim();
        }

        /// <summary>The full path, as this server reads it.</summary>
        public string Path { get; }

        /// <summary>The table the entry belongs in.</summary>
        public string TablePath { get; }

        /// <summary>The name of the entry itself.</summary>
        public string Name { get; }

        /// <summary>The data type, for example <c>Bool</c>.</summary>
        public string DataTypeName { get; }

        /// <summary>The logical address of a tag, or the value of a constant.</summary>
        public string Assignment { get; }
    }
}
