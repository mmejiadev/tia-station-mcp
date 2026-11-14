namespace TiaMcpServer.WatchTables
{
    /// <summary>Which of the two tables rows are written to.</summary>
    public enum WatchTableKind
    {
        /// <summary>A watch table: any address, and a value prepared to modify it with.</summary>
        Watch,

        /// <summary>The program's one force table: inputs and outputs only, and a value prepared to force them to.</summary>
        Force
    }
}
