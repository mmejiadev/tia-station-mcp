using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The values of several tags of a virtual controller.</summary>
    public sealed class ResponseSimulationTagValues : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One value per tag asked for, in the order asked for.</param>
        public ResponseSimulationTagValues(IReadOnlyList<ResponseSimulationTagValue> items)
        {
            Items = items;
        }

        /// <summary>
        /// One value per tag asked for, in the order asked for. They are read through one handle in
        /// one call, so they come from nearly the same moment — but the controller keeps scanning
        /// while they are read, so this is not a consistent snapshot of a scan.
        /// </summary>
        public IReadOnlyList<ResponseSimulationTagValue> Items { get; }
    }
}
