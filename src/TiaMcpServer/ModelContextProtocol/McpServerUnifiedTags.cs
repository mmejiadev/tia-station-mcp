using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading WinCC Unified HMI tags. Creating them is <c>CreateUnifiedTags</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedTags"), Description("Read a WinCC Unified HMI's connections and tags. Each connection says its partner PLC and whether it is integrated: only an integrated connection links HMI tags to PLC tags, and it is made by hand in TIA Portal (Openness cannot create one). Each tag gives its table and data type and, when linked, its connection, PLC, PLC tag and address. The HMI path is the Unified software's, e.g. 'HMI_MTP700/HMI_RT_1'.")]
        public static ResponseUnifiedTags GetUnifiedTags(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedTags(hmiPath);

                return new ResponseUnifiedTags(content)
                {
                    Message = $"'{hmiPath}': {(content["connections"] as JsonArray)?.Count ?? 0} connection(s), {(content["tags"] as JsonArray)?.Count ?? 0} tag(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the tags of '{hmiPath}'");
            }
        }
    }
}
