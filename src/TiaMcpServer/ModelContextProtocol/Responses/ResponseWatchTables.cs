using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The watch and force tables of a PLC program.</summary>
    public sealed class ResponseWatchTables : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="tables">One line per table: name, kind, rows, consistency.</param>
        public ResponseWatchTables(IReadOnlyList<string> tables)
        {
            Tables = tables;
        }

        /// <summary>
        /// One line per table, as <c>name | kind | rows | consistency</c>. The kind matters: a
        /// watch table can be created and deleted, and the single force table cannot.
        /// </summary>
        public IReadOnlyList<string> Tables { get; }
    }
}
