using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The rows of one watch or force table.</summary>
    public sealed class ResponseWatchTable : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="rows">One line per row that has an address.</param>
        public ResponseWatchTable(IReadOnlyList<string> rows)
        {
            Rows = rows;
        }

        /// <summary>
        /// One line per row, as <c>address | display format | monitor trigger | what is prepared</c>.
        /// Comment rows are not here: Openness exposes nothing on one but the means to delete it.
        /// </summary>
        public IReadOnlyList<string> Rows { get; }
    }
}
