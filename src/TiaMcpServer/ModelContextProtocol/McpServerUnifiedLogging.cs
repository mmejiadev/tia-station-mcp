using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading WinCC Unified logging. Creating it is <c>CreateUnifiedLogging</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedLogging"), Description("Read a WinCC Unified HMI's data logs, alarm logs and logging tags, in the shape CreateUnifiedLogging takes them. Each alarm log gives the alarm classes logged to it; each logging tag gives the HMI tag it logs, its data log, its mode (onChange, cyclic or onDemand) and, when cyclic, its cycle. A logging tag whose data log was deleted still names it, as TIA Portal keeps the name. The HMI path is the Unified software's, e.g. 'PC-System_1/HMI_RT_1'.")]
        public static ResponseUnifiedLogging GetUnifiedLogging(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedLogging(hmiPath);

                return new ResponseUnifiedLogging(content)
                {
                    Message = $"'{hmiPath}': {Count(content, "dataLogs")} data log(s), {Count(content, "alarmLogs")} alarm log(s), {Count(content, "loggingTags")} logging tag(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the logging of '{hmiPath}'");
            }
        }

        private static int Count(JsonObject content, string field)
        {
            return (content[field] as JsonArray)?.Count ?? 0;
        }
    }
}
