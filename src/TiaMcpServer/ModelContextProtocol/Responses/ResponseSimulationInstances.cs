using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The virtual controllers registered with the simulation runtime.</summary>
    public sealed class ResponseSimulationInstances : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="items">One entry per registered instance.</param>
        /// <param name="networkMode">How the runtime is reachable.</param>
        public ResponseSimulationInstances(IReadOnlyList<ResponseSimulationInstance> items, string networkMode)
        {
            Items = items;
            NetworkMode = networkMode;
        }

        /// <summary>One entry per registered instance.</summary>
        public IReadOnlyList<ResponseSimulationInstance> Items { get; }

        /// <summary>
        /// Softbus, TCPIPSingleAdapter, TCPIPMultipleAdapter, or Unavailable. Reported because a
        /// download that cannot connect says nothing about why, and this is the first thing to check.
        /// </summary>
        public string NetworkMode { get; }
    }
}
