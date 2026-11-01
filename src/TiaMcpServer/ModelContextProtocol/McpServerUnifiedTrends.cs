using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTrends;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>WinCC Unified trend controls: reading their trends and filling one just drawn.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedTrends"), Description("Read every trend control on a WinCC Unified HMI's screens: each trend area's trends with their source ('Level' for live values, 'Level:Level_log' for a logging tag's values), legend name, colour and mode, and the bottom time axis's range (factor and base). The HMI path is the Unified software's, e.g. 'PC-System_1/HMI_RT_1'.")]
        public static ResponseUnifiedTrends GetUnifiedTrends(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedTrends(hmiPath);

                return new ResponseUnifiedTrends(content)
                {
                    Message = $"'{hmiPath}': {Count(content, "trendControls")} trend control(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the trends of '{hmiPath}'");
            }
        }

        [McpServerTool(Name = "SetUnifiedTrends"), Description("Fill a WinCC Unified trend control that shows no trend yet, as DrawUnifiedScreen draws one ({\"type\": \"trendControl\"}), from JSON: {\"screen\": \"Start\", \"control\": \"Trend_1\", \"trends\": [{\"tag\": \"Level\", \"loggingTag\": \"Level_log\", \"name\": \"Nivell\", \"color\": \"#FF0000\", \"mode\": \"stepped\"}, {\"tag\": \"Pressure\"}], \"timeRange\": {\"factor\": 10, \"base\": \"minute\"}}. The trends go into the control's first trend area in order. A trend with a loggingTag (CreateUnifiedLogging makes one) shows that tag's logged values, one without shows live values only; the tag and logging tag must exist, as TIA Portal takes an unknown bare name; the tag is found by its name as TIA Portal spells it. mode is one of Points, Interpolated (the default), Stepped, Bar, Value; the time range factor runs 1 to 32767, its base Millisecond to Year. A control already showing a trend in any area is refused rather than overwritten; so is a timeRange on a control with no bottom time axis, or one not set by a time range. A name is written in the project's editing language. Everything is checked first; all or nothing; the control as it was is kept in the backup registry. A screen whose trend control shows trends is refused by DrawUnifiedScreen, whose backup cannot keep them: draw the screen first, set the trends last.")]
        public static ResponseUnifiedTrends SetUnifiedTrends(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("trends: the screen, the control and its trends as JSON, see the tool description")] string trends)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a change to approve.
                var list = UnifiedTrendListReader.Parse(trends);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("SetUnifiedTrends", target);
                var request = new Governance.ChangeRequest("SetUnifiedTrends", target, UnifiedTrendSummary.Of(list)).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeSetTrends(Portal.SetUnifiedTrends(hmiPath, list, backupDirectory), list),
                    () => new ResponseUnifiedTrends(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"setting Unified trends on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedTrends DescribeSetTrends(JsonObject control, UnifiedTrendList list)
        {
            return new ResponseUnifiedTrends(control)
            {
                Message = $"{list.Trends.Count} trend(s) set in '{list.Control}' on '{list.Screen}'. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["trends"] = list.Trends.Count
                }
            };
        }
    }
}
