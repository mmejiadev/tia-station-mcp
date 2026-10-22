using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Drawing a WinCC Unified screen. Reading it is <c>GetUnifiedScreen</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "DrawUnifiedScreen"), Description("Draw a WinCC Unified screen from a JSON description, creating the screen or replacing every item of one with that name: {\"name\": \"Main\", \"startScreen\": true, \"properties\": {\"Width\": 800, \"Height\": 480, \"BackColor\": \"#202020\"}, \"items\": [{\"type\": \"rectangle\", \"name\": \"Tank\", \"properties\": {\"Left\": 40, \"Top\": 60, \"Width\": 120, \"Height\": 200, \"BackColor\": \"blue\", \"Corners.TopLeftRadius\": 8}}, {\"type\": \"polyline\", \"name\": \"Pipe\", \"points\": [[0, 0], [60, 0], [60, 90]], \"properties\": {\"LineWidth\": 4}}, {\"type\": \"ioField\", \"name\": \"Level\", \"properties\": {\"Left\": 200, \"Top\": 60, \"Font.Size\": 14}, \"tags\": {\"ProcessValue\": \"Level\"}}, {\"type\": \"text\", \"name\": \"Title\", \"properties\": {\"Text\": \"Tank 1\"}}]}. Every shape, widget and control is drawable; GetUnifiedItemTypes lists them and, for one type, every property path, its value type and its enum values. Property names are Openness's own; parts are written 'Font.Size'; colours 'R, G, B', '#RRGGBB' or a name; texts are written in the project's editing language. 'tags' links a property to an existing HMI tag. The whole description is checked before anything changes, and the screen as it was is saved to the backup registry first, in this same JSON. Answers with the screen read back. Run CompileSoftware on the HMI path afterwards.")]
        public static ResponseUnifiedScreen DrawUnifiedScreen(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'HMI_MTP700/HMI_RT_1'")] string hmiPath,
            [Description("description: the screen as JSON, see the tool description")] string description)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a description that does not parse is a mistake in the call,
                // not a change anyone needs to approve or audit.
                var screen = UnifiedScreenDescriptionReader.Parse(description);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("DrawUnifiedScreen", target);
                var request = new Governance.ChangeRequest("DrawUnifiedScreen", target, screen.Name).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeUnifiedDrawing(Portal.DrawUnifiedScreen(hmiPath, screen, backupDirectory), screen),
                    () => new ResponseUnifiedScreen(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"drawing a Unified screen on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedScreen DescribeUnifiedDrawing(JsonObject drawn, UnifiedScreenDescription screen)
        {
            return new ResponseUnifiedScreen(drawn)
            {
                Message = $"Screen '{screen.Name}' drawn with {screen.Items.Count} item(s). {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["items"] = screen.Items.Count
                }
            };
        }
    }
}
