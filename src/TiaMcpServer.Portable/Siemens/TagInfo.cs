namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One entry of a tag table: a tag bound to an address, or a constant bound to a value.
    /// </summary>
    /// <remarks>
    /// Both kinds live in the same table and both are what SCL refers to by name, so they are
    /// listed together rather than in two places a reader has to join. What distinguishes them is
    /// <see cref="Kind"/>, and it is a word rather than a flag because a caller reading the list
    /// should not have to know which boolean means what.
    ///
    /// <see cref="Assignment"/> is deliberately one field. A tag holds an address and a constant
    /// holds a value; they are the same column of the same table in TIA Portal's own editor, and
    /// splitting them would leave every row with one empty half.
    /// </remarks>
    public sealed class TagInfo
    {
        /// <summary>The kind of entry a tag is.</summary>
        public const string TagKind = "Tag";

        /// <summary>The kind of entry a user constant is.</summary>
        public const string ConstantKind = "Constant";

        /// <summary>Creates a tag table entry description.</summary>
        /// <param name="name">The name SCL refers to it by.</param>
        /// <param name="kind"><see cref="TagKind"/> or <see cref="ConstantKind"/>.</param>
        /// <param name="dataTypeName">The data type, as TIA Portal spells it back.</param>
        /// <param name="assignment">The logical address of a tag, or the value of a constant.</param>
        public TagInfo(string name, string kind, string dataTypeName, string assignment)
        {
            Name = name;
            Kind = kind;
            DataTypeName = dataTypeName;
            Assignment = assignment;
        }

        /// <summary>The name SCL refers to it by.</summary>
        public string Name { get; }

        /// <summary><see cref="TagKind"/> or <see cref="ConstantKind"/>.</summary>
        public string Kind { get; }

        /// <summary>
        /// The data type as TIA Portal spells it back, which is not always as it was written: it
        /// normalises case, so <c>bool</c> comes back as <c>Bool</c>.
        /// </summary>
        public string DataTypeName { get; }

        /// <summary>The logical address of a tag, or the value of a constant.</summary>
        public string Assignment { get; }

        /// <summary>Whether this entry is a constant rather than a tag.</summary>
        public bool IsConstant => Kind == ConstantKind;
    }
}
