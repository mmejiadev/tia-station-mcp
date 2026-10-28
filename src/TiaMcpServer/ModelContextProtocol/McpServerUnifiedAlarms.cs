using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Reading WinCC Unified alarms. Creating them is <c>CreateUnifiedAlarms</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedAlarms"), Description("Read a WinCC Unified HMI's alarm classes, discrete alarms and analog alarms, in the shape CreateUnifiedAlarms takes them, texts in the project's editing language. Each class gives its priority, state machine and the colours of its four states; the 18 system classes every HMI has are marked system. Each alarm gives its tag (and bit, for a discrete one), trigger or condition and value, class, priority, text, and info, origin and area when set. Read only: an alarm's id, which TIA Portal gives, and a class's system flag. The HMI path is the Unified software's, e.g. 'PC-System_1/HMI_RT_1'.")]
        public static ResponseUnifiedAlarms GetUnifiedAlarms(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedAlarms(hmiPath);

                return new ResponseUnifiedAlarms(content)
                {
                    Message = $"'{hmiPath}': {(content["classes"] as JsonArray)?.Count ?? 0} alarm class(es), {(content["discrete"] as JsonArray)?.Count ?? 0} discrete and {(content["analog"] as JsonArray)?.Count ?? 0} analog alarm(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the alarms of '{hmiPath}'");
            }
        }
    }
}
