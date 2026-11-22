using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>A change to some rows of one PLC tag table, leaving the others as they are.</summary>
    /// <remarks>Applied in this order: <see cref="Remove"/>, <see cref="Set"/>, <see cref="Add"/>.</remarks>
    public sealed class TagTableEdit
    {
        /// <summary>Creates an edit.</summary>
        /// <param name="table">The table's path, e.g. <c>Default tag table</c> or <c>Cell/IO</c>.</param>
        /// <param name="remove">The tags to delete, by name.</param>
        /// <param name="rows">The tags to change in place, and the tags to add.</param>
        public TagTableEdit(string table, IReadOnlyList<string> remove, (IReadOnlyList<TagTableRow> Set, IReadOnlyList<TagTableRow> Add) rows)
        {
            Table = table;
            Remove = remove;
            Set = rows.Set;
            Add = rows.Add;
        }

        /// <summary>The table's path.</summary>
        public string Table { get; }

        /// <summary>The tags to delete, by name.</summary>
        public IReadOnlyList<string> Remove { get; }

        /// <summary>The tags to change in place: only what each gives.</summary>
        public IReadOnlyList<TagTableRow> Set { get; }

        /// <summary>The tags to add, each with a data type and an address.</summary>
        public IReadOnlyList<TagTableRow> Add { get; }

        /// <summary>The names of the tags already in the table this edit changes or deletes.</summary>
        public IReadOnlyList<string> Touched => Remove.Concat(Set.Select(row => row.Name)).ToList();
    }
}
