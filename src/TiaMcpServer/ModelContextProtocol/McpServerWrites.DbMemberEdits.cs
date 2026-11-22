using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Changing some members of a global data block. Replacing the whole block is <c>ImportBlock</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "EditDbMembers"), Description("Change some members of a global data block and leave the others as they are, from JSON: {\"block\": \"DADES COMPARTIR\", \"remove\": [\"Old\"], \"set\": [{\"name\": \"Count\", \"startValue\": 9}], \"add\": [{\"name\": \"Extra\", \"dataType\": \"Int\", \"startValue\": 7}]}. Every part is optional, but one is needed. 'remove' deletes members by name; 'set' changes a member's data type or start value in place (only what it gives; a new data type with no start value drops the old one); 'add' appends members, each with a data type. A member is named in one list at most; the block is a path as GetBlocks prints it. A start value is written as TIA Portal writes it: 9, 21.5, true, and for a String its quotes, 'abc'. Only members at the block's own level of a global DB: a struct's members, an array's per-element start values and instance DBs are left to TIA Portal. The block must be consistent (compile first) and TIA Portal offline. The program's blocks and types are exported to the backup registry, the block is exported, changed and imported again - it keeps its number and its start values - and the software is compiled: if that finds more errors than before, or TIA Portal refuses the block, it is imported back as it was. Removing or renaming a member a block reads is therefore refused by the compile. The values a DB holds in a CPU are not carried: a download of a DB whose structure changed initialises it.")]
        public static ResponseMessage EditDbMembers(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_1'")] string softwarePath,
            [Description("edit: the change as JSON, see the tool description")] string edit)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                // Read before the guard: an edit that does not parse is a mistake in the call, not a change to approve.
                var change = DbMemberEditReader.Parse(edit);
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("EditDbMembers", target);
                var request = new Governance.ChangeRequest("EditDbMembers", target, $"{change.Block}: remove {change.Remove.Count}, set {change.Set.Count}, add {change.Add.Count}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeDbMemberEdit(Portal.EditDbMembers(softwarePath, change, backupDirectory), change), () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"editing data block on '{softwarePath}'");
            }
        }

        private static ResponseMessage DescribeDbMemberEdit(JsonObject edited, DbMemberEdit change)
        {
            return new ResponseMessage
            {
                Message = $"Data block '{change.Block}' edited: {change.Remove.Count} removed, {change.Set.Count} changed, {change.Add.Count} added. The software was compiled after it.",
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
