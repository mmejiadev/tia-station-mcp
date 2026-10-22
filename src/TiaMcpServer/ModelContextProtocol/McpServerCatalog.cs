using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The hardware catalogue, read so that a device is added from what this installation offers.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "FindCatalogEntries"), Description("Search TIA Portal's hardware catalogue by order number or product name, e.g. '6AV2 124-0JC01', 'TP900 Comfort', 'MTP700', '1215C', 'PC station', 'WinCC Unified PC'. Each entry gives the typeIdentifier to pass to AddDevice, to PlugModule for a module, or to AddPcStation for a PC HMI runtime (never PlugModule: a PC station's rack refuses it), one per firmware version and, for panels, per orientation. Use it before AddDevice instead of writing an order number from memory. The first search of a session loads the catalogue and takes about half a minute. Needs a connection, no project.")]
        public static ResponseCatalogEntries FindCatalogEntries(
            [Description("filter: an order number, a product name or part of either, e.g. '6AV2 124-0JC01' or 'TP900 Comfort'")] string filter)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var result = Portal.FindCatalogEntries(filter);

                return new ResponseCatalogEntries(result)
                {
                    Message = result.IsTruncated
                        ? $"{result.TotalFound} catalogue entries match '{filter}'; the first {result.Entries.Count} are listed. Narrow the filter to see the rest"
                        : $"{result.TotalFound} catalogue entries match '{filter}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to search the hardware catalogue for '{filter}'");
            }
        }
    }
}
