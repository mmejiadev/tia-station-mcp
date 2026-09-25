using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Which addresses somebody will be looking at. The writes that go with these reads live in
    /// the file that changes things, like every write.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetWatchTables"), Description("List the watch and force tables of a PLC program. Each line is 'name | kind | rows | consistency'. Watch tables are created and deleted freely; a program has exactly one force table, which TIA Portal makes and Openness can neither create nor delete - take its name from here. These are offline objects: they say which addresses somebody will look at, and hold no value read from a running CPU.")]
        public static ResponseWatchTables GetWatchTables(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var tables = Portal.GetWatchTables(softwarePath);

                return new ResponseWatchTables(tables.Select(table => table.Line).ToList())
                {
                    Message = tables.Count == 0
                        ? $"'{softwarePath}' has no watch or force table"
                        : $"'{softwarePath}' has {tables.Count} table(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the watch tables of '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "GetWatchTable"), Description("Read the rows of one watch or force table, named as GetWatchTables prints it. Each line is 'address | display format | monitor trigger | what is prepared'. A row that only watches says so; a row with a value prepared shows the value, and for a watch table the trigger that would apply it. Nothing here was read from a machine - a prepared value takes effect only when somebody opens the table against a connected CPU and activates it.")]
        public static ResponseWatchTable GetWatchTable(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("tablePath: the table, e.g. 'Cell checks' or 'Group/Cell checks'")] string tablePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var rows = Portal.GetWatchTable(softwarePath, tablePath);

                return new ResponseWatchTable(rows.Select(row => row.Line).ToList())
                {
                    Message = rows.Count == 0
                        ? $"'{tablePath}' has no row with an address"
                        : $"'{tablePath}' has {rows.Count} row(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the rows of '{tablePath}' in '{softwarePath}'");
            }
        }

        /// <remarks>
        /// A read as far as the project is concerned: it writes a file and changes nothing in TIA
        /// Portal, so it does not go through the guard. <c>ExportBlock</c> sits the same way.
        /// </remarks>
        [McpServerTool(Name = "ExportWatchTable"), Description("Export one watch table to a SimaticML document. This is half of the only mechanism there is for putting rows in a table: Openness can create a table and a comment row and nothing else, so a table with addresses is one that was built in TIA Portal, exported here, and imported with ImportWatchTables wherever it is needed. Siemens publishes no schema for the format, which makes an exported file the only specification of it. Writes a file and changes nothing in the project.")]
        public static ResponseMessage ExportWatchTable(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("tablePath: the watch table, as GetWatchTables names it")] string tablePath,
            [Description("exportPath: the .xml file to write")] string exportPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var written = Portal.ExportWatchTable(softwarePath, tablePath, exportPath);

                return new ResponseMessage
                {
                    Message = $"'{tablePath}' was written to {written}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["exportPath"] = written
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to export watch table '{tablePath}' from '{softwarePath}'");
            }
        }
    }
}
