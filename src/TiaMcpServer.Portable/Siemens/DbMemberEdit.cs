using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>A change to some members of one global data block, leaving the others as they are.</summary>
    /// <remarks>Applied in this order: <see cref="Remove"/>, <see cref="Set"/>, <see cref="Add"/>.</remarks>
    public sealed class DbMemberEdit
    {
        /// <summary>Creates an edit.</summary>
        /// <param name="block">The data block's path, e.g. <c>DADES COMPARTIR</c> or <c>Cell/Data</c>.</param>
        /// <param name="remove">The members to delete, by name.</param>
        /// <param name="rows">The members to change in place, and the members to add.</param>
        public DbMemberEdit(string block, IReadOnlyList<string> remove, (IReadOnlyList<DbMemberRow> Set, IReadOnlyList<DbMemberRow> Add) rows)
        {
            Block = block;
            Remove = remove;
            Set = rows.Set;
            Add = rows.Add;
        }

        /// <summary>The data block's path.</summary>
        public string Block { get; }

        /// <summary>The members to delete, by name.</summary>
        public IReadOnlyList<string> Remove { get; }

        /// <summary>The members to change in place: only what each gives.</summary>
        public IReadOnlyList<DbMemberRow> Set { get; }

        /// <summary>The members to add, each with a data type.</summary>
        public IReadOnlyList<DbMemberRow> Add { get; }

        /// <summary>The names of the members already in the block this edit changes or deletes.</summary>
        public IReadOnlyList<string> Touched => Remove.Concat(Set.Select(row => row.Name)).ToList();
    }
}
