using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading WinCC Unified HMI tags. Creating them is <c>CreateUnifiedTags</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedTags"), Description("Read a WinCC Unified HMI's connections and tags. Each connection says its partner PLC and whether it is integrated: only an integrated connection links HMI tags to PLC tags, and it is made by hand in TIA Portal (Openness cannot create one). Each tag gives its table (groups as 'Group/Table') and data type and, when linked, its connection, PLC, PLC tag and address, then only the settings that differ from TIA Portal's defaults, in the shape CreateUnifiedTags takes them: comment, initialValue, persistent, maxLength (always, for a WString), limits, and for a linked tag acquisition, scaling and substitute. Two more are read only, and CreateUnifiedTags refuses them: tagType (array or udt; a PLC array or UDT linked whole) and thresholds, which Openness V20 cannot create. tableGroups lists the tag table groups by path, empty ones included. systemTags lists the HMI's system tags (@...) with their data types. The HMI path is the Unified software's, e.g. 'HMI_MTP700/HMI_RT_1'.")]
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
