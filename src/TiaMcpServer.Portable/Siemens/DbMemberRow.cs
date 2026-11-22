namespace TiaMcpServer.Siemens
{
    /// <summary>One member of a global data block an edit adds or changes: its name, and what of it is given.</summary>
    /// <remarks>A member added gives a data type; a member changed gives only what changes, null for the rest.</remarks>
    public sealed class DbMemberRow
    {
        /// <summary>Creates a row.</summary>
        /// <param name="name">The member's name.</param>
        /// <param name="dataType">Its data type, e.g. <c>Int</c>, <c>Real</c>, <c>String[10]</c>, or null to leave it.</param>
        /// <param name="startValue">Its start value as TIA Portal writes it, e.g. <c>5</c>, <c>21.5</c>, <c>true</c>, <c>'abc'</c>, or null to leave it.</param>
        public DbMemberRow(string name, string? dataType, string? startValue)
        {
            Name = name;
            DataType = dataType;
            StartValue = startValue;
        }

        /// <summary>The member's name.</summary>
        public string Name { get; }

        /// <summary>Its data type, or null to leave it.</summary>
        public string? DataType { get; }

        /// <summary>Its start value, or null to leave it.</summary>
        public string? StartValue { get; }
    }
}
