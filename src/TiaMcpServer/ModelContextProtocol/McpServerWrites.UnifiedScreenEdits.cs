using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Changing some items of a WinCC Unified screen. Drawing a whole one is <c>DrawUnifiedScreen</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "EditUnifiedScreen"), Description("Change some items of an existing WinCC Unified screen and leave every other item as it is - faceplates and Custom Web Controls included, which DrawUnifiedScreen cannot redraw. JSON: {\"screen\": \"Main\", \"properties\": {\"BackColor\": \"#202020\"}, \"remove\": [\"Old_Button\"], \"set\": [{\"name\": \"Level\", \"properties\": {\"Left\": 300, \"Font.Size\": 16}}], \"draw\": [{\"type\": \"button\", \"name\": \"Start\", \"properties\": {\"Left\": 40, \"Top\": 400, \"Text\": \"Start\"}, \"events\": [{\"event\": \"down\", \"function\": \"setBit\", \"tag\": \"Motor\"}]}]}. Every part is optional, but one is needed. 'properties' sets the screen's own; 'remove' deletes items by name; 'set' changes properties of an item in place (it keeps its place among the others; its links, events and animations stay); 'draw' adds an item, or replaces the one of that name, written exactly as an item of DrawUnifiedScreen (tags, events, animations, points) and named; a drawn item comes on top of the others. An item is named in one list at most. Applied in that order. Everything is checked first - items there, properties, HMI tags, text lists, scripts rehearsed - and the items touched and the screen's properties are read into the backup registry; an item whose backup could not put it back (a faceplate removed, say) is refused, nothing changed. If TIA Portal refuses part of the edit, what was done is undone from that backup (an item put back comes on top). GetUnifiedScreen reads the screen to see names and properties. Compile the HMI afterwards.")]
        public static ResponseUnifiedScreen EditUnifiedScreen(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("edit: the change as JSON, see the tool description")] string edit)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: an edit that does not parse is a mistake in the call, not a change to approve.
                var change = UnifiedScreenEditReader.Parse(edit);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("EditUnifiedScreen", target);
                var request = new Governance.ChangeRequest("EditUnifiedScreen", target, $"{change.Screen}: remove {change.Remove.Count}, set {change.Set.Count}, draw {change.Draw.Count}").WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeUnifiedEdit(Portal.EditUnifiedScreen(hmiPath, change, backupDirectory), change),
                    () => new ResponseUnifiedScreen(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"editing a Unified screen on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedScreen DescribeUnifiedEdit(JsonObject edited, UnifiedScreenEdit change)
        {
            return new ResponseUnifiedScreen(edited)
            {
                Message = $"Screen '{change.Screen}' edited: {change.Remove.Count} removed, {change.Set.Count} changed in place, {change.Draw.Count} drawn. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["removed"] = change.Remove.Count,
                    ["set"] = change.Set.Count,
                    ["drawn"] = change.Draw.Count
                }
            };
        }
    }
}
