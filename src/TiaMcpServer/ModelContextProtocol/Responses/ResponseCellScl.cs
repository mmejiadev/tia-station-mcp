using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>SCL generated for a cell, not yet written anywhere.</summary>
    public sealed class ResponseCellScl : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="cellName">The cell the source describes.</param>
        /// <param name="stationNames">Its stations, in the order a piece visits them.</param>
        /// <param name="scl">The SCL source, station pattern first.</param>
        public ResponseCellScl(string cellName, IReadOnlyList<string> stationNames, string scl)
        {
            CellName = cellName;
            StationNames = stationNames;
            Scl = scl;
        }

        /// <summary>The cell the source describes.</summary>
        public string CellName { get; }

        /// <summary>Its stations, in the order a piece visits them.</summary>
        public IReadOnlyList<string> StationNames { get; }

        /// <summary>The SCL source, station pattern first and coordinator second.</summary>
        /// <remarks>
        /// The order is not cosmetic: the coordinator declares instances of the station, so the
        /// station type has to exist before it. One source rather than two because <c>WriteScl</c>
        /// generates every block a source declares, in the order it reads them.
        /// </remarks>
        public string Scl { get; }
    }
}
