using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Reads from an OPC UA server. Only reads.
    /// </summary>
    /// <remarks>
    /// No write, no method call, no subscription. Reading a running machine is the capability this
    /// phase adds; commanding one is phase 11, and a reader that could also write would be a
    /// commanding tool that nobody decided to build.
    ///
    /// Callers check <see cref="OpcUaAccessPolicy"/> first. The reader checks the server's
    /// certificate against <see cref="ServerCertificatePins"/> itself, because only the reader sees
    /// the certificate.
    /// </remarks>
    public interface IOpcUaReader
    {
        /// <summary>Lists the objects and variables directly under a node.</summary>
        /// <param name="endpoint">The server.</param>
        /// <param name="nodeId">The parent, or null for the server's Objects folder.</param>
        /// <param name="cancellationToken">Cancels the connection and the browse.</param>
        /// <returns>The children, in the order the server returned them.</returns>
        public Task<IReadOnlyList<OpcUaNode>> BrowseAsync(OpcUaEndpoint endpoint, string? nodeId, CancellationToken cancellationToken);

        /// <summary>Reads the current value of each node.</summary>
        /// <param name="endpoint">The server.</param>
        /// <param name="nodeIds">The variables to read.</param>
        /// <param name="cancellationToken">Cancels the connection and the read.</param>
        /// <returns>One reading per node, in the order asked, bad statuses included.</returns>
        public Task<IReadOnlyList<OpcUaReading>> ReadAsync(OpcUaEndpoint endpoint, IReadOnlyList<string> nodeIds, CancellationToken cancellationToken);
    }
}
