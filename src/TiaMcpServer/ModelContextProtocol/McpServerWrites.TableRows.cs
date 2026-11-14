using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Rows in watch tables and in the force table. Part of McpServerWrites: both replace what a table holds, so both
    /// go through the guard and export every table of the program first.
    /// </remarks>
    public static partial class McpServer
    {
        private const string RowsExample = "{\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\", \"value\": \"TRUE\", \"comment\": \"Lamp\"}, {\"address\": \"%QW2\", \"format\": \"DEC_signed\", \"value\": \"100\"}]}";

        [McpServerTool(Name = "WriteForceTable"), Description("Replace the rows of a PLC program's force table, offline, from JSON: " + RowsExample + ". Each row names an input or output (%I, %Q: an S7-1500 forces nothing else), a display format (Bool for a bit; DEC_signed, DEC_unsigned, Hex or Float for a byte, word or double word) and the value it is forced to (TRUE/FALSE, -12, 12, 16#0C, 1.5); a comment is optional. Absolute addresses only: a symbolic name is not measured yet. TIA Portal adds ':P' to them, as forcing acts on the periphery. The rows are PREPARED, NOT FORCED: Openness cannot tick the force box, so forcing is started online in TIA Portal. Everything is checked first and nothing is written if a row is refused; every table of the program is exported to the backup registry first, as the import replaces all the force table's rows. Refused while TIA Portal is online with the CPU (GoOffline).")]
        public static ResponseMessage WriteForceTable(
            [Description("softwarePath: the PLC's software, e.g. 'PLC_1'")] string softwarePath,
            [Description("rows: the rows as JSON, see the tool description")] string rows)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("WriteForceTable", target);
                var request = new Governance.ChangeRequest("WriteForceTable", target, rows).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeTableRows(Portal.WriteForceTable(softwarePath, rows, backupDirectory), $"The force table of '{softwarePath}'", "Prepared, not forced: tick the boxes online in TIA Portal to force."),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"writing the force table of '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "WriteWatchTable"), Description("Write a watch table at a PLC program's root with exactly these rows, offline, from JSON: " + RowsExample + ". A table of that name is created, or its rows are replaced. Each row names an input, output or memory address (%I0.0, %QW2, %MD4; data blocks and symbolic names are not measured yet), a display format (Bool for a bit; DEC_signed, DEC_unsigned, Hex or Float otherwise) and, optionally, a value prepared to modify it with (TRUE/FALSE, -12, 12, 16#0C, 1.5); without a value the row only watches. Nothing is modified: Openness cannot tick the modify box, so modifying is done online in TIA Portal. Everything is checked first and nothing is written if a row is refused; every table of the program is exported to the backup registry first. Refused while TIA Portal is online with the CPU (GoOffline).")]
        public static ResponseMessage WriteWatchTable(
            [Description("softwarePath: the PLC's software, e.g. 'PLC_1'")] string softwarePath,
            [Description("tableName: the table's name at the program's root, e.g. 'Motor checks'")] string tableName,
            [Description("rows: the rows as JSON, see the tool description")] string rows)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("WriteWatchTable", target);
                var request = new Governance.ChangeRequest("WriteWatchTable", target, $"{tableName}: {rows}").WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeTableRows(Portal.WriteWatchTable(softwarePath, tableName, rows, backupDirectory), $"Watch table '{tableName}' of '{softwarePath}'", "Nothing was modified: values are applied online in TIA Portal."),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"writing watch table '{tableName}' of '{softwarePath}'");
            }
        }

        private static ResponseMessage DescribeTableRows(IReadOnlyList<WatchEntryInfo> rows, string table, string note)
        {
            return new ResponseMessage
            {
                Message = $"{table} holds {rows.Count} row(s): {string.Join("; ", rows.Select(row => row.Line))}. {note}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["rows"] = new JsonArray(rows.Select(row => (JsonNode)row.Line).ToArray())
                }
            };
        }
    }
}
