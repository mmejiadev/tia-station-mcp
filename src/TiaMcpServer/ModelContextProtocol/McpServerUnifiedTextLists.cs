using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading WinCC Unified text lists. Writing one is <c>CreateUnifiedTextList</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedTextLists"), Description("Read every text list of a WinCC Unified HMI: per list its entries, each a value (or a from..to range made in the TIA Portal editor) and its text in the project's editing language. A symbolic IO field shows a list through its ResourceList property, and a text through a 'list' animation of DrawUnifiedScreen. Openness V20 has no graphic lists for Unified. The HMI path is the Unified software's, e.g. 'HMI_MTP700/HMI_RT_1'.")]
        public static ResponseUnifiedTextLists GetUnifiedTextLists(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedTextLists(hmiPath);

                return new ResponseUnifiedTextLists(content)
                {
                    Message = $"'{hmiPath}': {(content["textLists"] as JsonArray)?.Count ?? 0} text list(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the text lists of '{hmiPath}'");
            }
        }
    }
}
