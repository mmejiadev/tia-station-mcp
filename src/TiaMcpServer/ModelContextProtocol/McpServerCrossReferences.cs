using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Who uses what. A read, and the one worth doing before any write to a block: the caller sees
    /// what the block does from its source, and nothing at all about what depends on it.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetCrossReferences"), Description("Find out what uses a block and what it uses, before changing it. Returns three lists: what calls or reads the block - these break if its interface changes - what the block itself calls or reads, and any other relation TIA Portal reports. Each line is 'name | path | type | relation | access | location | address'. An empty UsedBy list means nothing in the program calls the block, which is the answer to ask for before deleting one.")]
        public static ResponseCrossReferences GetCrossReferences(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Cell/FB_Station'")] string blockPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var report = Portal.GetCrossReferences(softwarePath, blockPath);

                return new ResponseCrossReferences(report)
                {
                    Message = $"'{blockPath}': {report.Summary}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the cross references of '{blockPath}'");
            }
        }
    }
}
