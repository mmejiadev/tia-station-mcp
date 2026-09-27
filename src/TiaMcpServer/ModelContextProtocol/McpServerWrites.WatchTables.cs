using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Watch tables: creating one, and importing the rows Openness cannot create.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateWatchTable"), Description("Create a watch table in a PLC program. The group above it has to exist already; leave it out to put the table at the program's root. A table of that name already there is refused rather than replaced. The program's tables are exported to the backup registry first. A watch table is offline: it says which addresses somebody will look at when they open it in TIA Portal.")]
        public static ResponseMessage CreateWatchTable(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("tablePath: the table, e.g. 'Cell checks' or 'Group/Cell checks'")] string tablePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("CreateWatchTable", target);
                var request = new Governance.ChangeRequest("CreateWatchTable", target, tablePath)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var table = Portal.CreateWatchTable(softwarePath, tablePath, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{table.Name}' is in '{softwarePath}' and holds {table.EntryCount} row(s)",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["tablePath"] = table.Name
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create watch table '{tablePath}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "ImportWatchTables"), Description("Import watch tables into a PLC program from a SimaticML document, as ExportWatchTable writes one. THIS IS THE ONLY WAY TO PUT ROWS IN A TABLE: Openness will create a table and a comment row and nothing else, so a table with addresses in it is one that was exported from somewhere and imported here. A table of the same name is replaced rather than duplicated, and the program's tables are exported to the backup registry first, which is what the replaced one held. Tables land at the program's root.")]
        public static ResponseMessage ImportWatchTables(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("documentPath: the .xml document to import")] string documentPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("ImportWatchTables", target);
                var request = new Governance.ChangeRequest("ImportWatchTables", target, documentPath)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var imported = Portal.ImportWatchTables(softwarePath, documentPath, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = imported.Count == 0
                                ? $"'{documentPath}' produced no table"
                                : $"'{softwarePath}' now holds {string.Join(", ", imported)}",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["count"] = imported.Count
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to import watch tables from '{documentPath}' into '{softwarePath}'");
            }
        }
    }
}
