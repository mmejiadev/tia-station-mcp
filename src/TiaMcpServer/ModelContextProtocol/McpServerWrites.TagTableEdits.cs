using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Changing some rows of a PLC tag table. Creating one tag is <c>CreateTag</c>, which never overwrites.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "EditTagTable"), Description("Change some rows of an existing PLC tag table and leave the others as they are, from JSON: {\"table\": \"Default tag table\", \"remove\": [\"Old_Lamp\"], \"set\": [{\"name\": \"Start\", \"address\": \"%I0.1\", \"comment\": \"Start button, NO\"}], \"add\": [{\"name\": \"Stop\", \"dataType\": \"Bool\", \"address\": \"%I0.2\", \"comment\": \"Stop button, NC\"}]}. Every part is optional, but one is needed. 'remove' deletes tags by name; 'set' changes a tag's data type, address or comment in place (only what it gives); 'add' creates tags, each with a data type and an address. A tag is named in one list at most; the table is a path as GetTagTables prints it. Comments are in the project's editing language. Everything named is checked first; the program's tag tables are exported to the backup registry; if TIA Portal refuses a type or an address, what was done is undone - a tag deleted is created again as it was. WATCH THE DATA TYPE: TIA Portal stores a type that does not exist without complaining (see CreateTag). Only tags: constants are CreateConstant's.")]
        public static ResponseMessage EditTagTable(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_1'")] string softwarePath,
            [Description("edit: the change as JSON, see the tool description")] string edit)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                // Read before the guard: an edit that does not parse is a mistake in the call, not a change to approve.
                var change = TagTableEditReader.Parse(edit);
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("EditTagTable", target);
                var request = new Governance.ChangeRequest("EditTagTable", target, $"{change.Table}: remove {change.Remove.Count}, set {change.Set.Count}, add {change.Add.Count}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeTagTableEdit(Portal.EditTagTable(softwarePath, change, backupDirectory), change), () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"editing tag table on '{softwarePath}'");
            }
        }

        private static ResponseMessage DescribeTagTableEdit(JsonObject edited, TagTableEdit change)
        {
            return new ResponseMessage
            {
                Message = $"Tag table '{change.Table}' edited: {change.Remove.Count} removed, {change.Set.Count} changed, {change.Add.Count} added. Compile the software to check them.",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["result"] = edited
                }
            };
        }
    }
}
