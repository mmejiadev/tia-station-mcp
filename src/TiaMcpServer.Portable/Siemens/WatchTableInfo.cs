namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One watch or force table of a PLC program, as it stands in the project.
    /// </summary>
    /// <remarks>
    /// The two kinds are listed together because a caller looking for "the tables of this PLC"
    /// wants both, and told apart by <see cref="Kind"/> because almost nothing else about them is
    /// the same: watch tables are created and deleted freely, and a program has exactly one force
    /// table, which Openness can neither create nor delete.
    ///
    /// Everything here is the *offline* project. A watch table says which addresses somebody will
    /// look at when they open it in TIA Portal; it holds no value read from a machine, and
    /// Openness offers no way to read one. That is phase 9 and a different protocol.
    /// </remarks>
    public sealed class WatchTableInfo
    {
        /// <summary>The kind of a table that monitors addresses.</summary>
        public const string WatchKind = "Watch";

        /// <summary>The kind of the single table that forces them.</summary>
        public const string ForceKind = "Force";

        /// <summary>Creates a table description.</summary>
        /// <param name="name">The table's name in the project.</param>
        /// <param name="kind"><see cref="WatchKind"/> or <see cref="ForceKind"/>.</param>
        /// <param name="entryCount">How many rows it holds, comment rows included.</param>
        /// <param name="isConsistent">Whether TIA Portal considers it consistent.</param>
        public WatchTableInfo(string name, string kind, int entryCount, bool isConsistent)
        {
            Name = name;
            Kind = kind;
            EntryCount = entryCount;
            IsConsistent = isConsistent;
        }

        /// <summary>The table's name in the project.</summary>
        public string Name { get; }

        /// <summary>Whether it watches or forces.</summary>
        public string Kind { get; }

        /// <summary>How many rows it holds.</summary>
        public int EntryCount { get; }

        /// <summary>Whether TIA Portal considers it consistent.</summary>
        public bool IsConsistent { get; }

        /// <summary>The table as one line.</summary>
        public string Line => $"{Name} | {Kind} | {EntryCount} row(s) | {(IsConsistent ? "consistent" : "inconsistent")}";
    }
}
