using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Tag tables, tags and constants.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateTagTable"), Description("Create a tag table in a PLC program, and the groups above it when they are missing. Asking for a table that already exists reports it rather than failing. The program's tag tables are exported to the backup registry first.")]
        public static ResponseMessage CreateTagTable(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("tablePath: full path of the table, e.g. 'IO' or 'Cell/IO'")] string tablePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("CreateTagTable", target);
                var request = new Governance.ChangeRequest("CreateTagTable", target, tablePath)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var table = Portal.CreateTagTable(softwarePath, tablePath, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{table.Path}' holds {table.TagCount} tag(s) and {table.UserConstantCount} constant(s)",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["tablePath"] = table.Path
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create tag table '{tablePath}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "CreateTag"), Description("Create a PLC tag bound to a logical address. The path is the table's path plus the tag's name, as GetTagTables and GetTags print them. Creating the same tag twice reports the one that is there; a name already taken by a different tag or constant is refused rather than overwritten. WATCH THE DATA TYPE: TIA Portal stores a type that does not exist without complaining, and compiling the software does not catch it either, so copy the spelling from GetTags of a tag that already works. The tag tables are exported to the backup registry first.")]
        public static ResponseMessage CreateTag(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("tagPath: table path plus tag name, e.g. 'Default tag table/Start' or 'Cell/IO/Start'")] string tagPath,
            [Description("dataType: the data type, e.g. 'Bool', 'Int', 'Real'")] string dataType,
            [Description("address: the logical address, e.g. '%I0.0', '%Q0.1', '%MW10'")] string address)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                return CreateTagTableEntry(
                    softwarePath,
                    new TagDefinition(tagPath, dataType, address),
                    "CreateTag",
                    Portal.CreateTag);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create tag '{tagPath}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "CreateConstant"), Description("Create a PLC user constant bound to a value. The path is the table's path plus the constant's name. Creating the same constant twice reports the one that is there; a name already taken by a different constant or tag is refused rather than overwritten. The tag tables are exported to the backup registry first.")]
        public static ResponseMessage CreateConstant(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("constantPath: table path plus constant name, e.g. 'Default tag table/CYCLE_MS'")] string constantPath,
            [Description("dataType: the data type, e.g. 'Int', 'Time', 'Real'")] string dataType,
            [Description("value: the value, e.g. '500' or 'T#1s'")] string value)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                return CreateTagTableEntry(
                    softwarePath,
                    new TagDefinition(constantPath, dataType, value),
                    "CreateConstant",
                    Portal.CreateConstant);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create constant '{constantPath}' in '{softwarePath}'");
            }
        }

        /// <remarks>
        /// A tag and a constant differ in one Openness call and in nothing else that matters here:
        /// same target, same backup, same guard, same reading back of what the project ended up
        /// holding. Writing that twice would be two places for one rule to be forgotten in.
        ///
        /// The entry is described from the project rather than from the arguments, which is how a
        /// caller learns that TIA spelled its data type back differently.
        /// </remarks>
        private static ResponseMessage CreateTagTableEntry(
            string softwarePath,
            TagDefinition definition,
            string operation,
            Func<string, TagDefinition, string, TagInfo> create)
        {
            var target = ChangeTarget.Program(softwarePath);
            var backupDirectory = Backups.Allocate(operation, target);
            var request = new Governance.ChangeRequest(operation, target, definition.Path)
                .WithBackup(backupDirectory);

            return GuardedTool.Run(
                GuardedWrites,
                request,
                () =>
                {
                    var entry = create(softwarePath, definition, backupDirectory);

                    return new ResponseMessage
                    {
                        Message = $"'{definition.Path}' is {entry.DataTypeName} at '{entry.Assignment}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["dataType"] = entry.DataTypeName,
                            ["assignment"] = entry.Assignment
                        }
                    };
                },
                () => new ResponseMessage());
        }
    }
}
