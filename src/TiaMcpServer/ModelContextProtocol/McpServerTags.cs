using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The names a program uses: which tag tables exist, and what is in one.
    ///
    /// These two are the manual for the three write tools beside them. A tag is addressed by its
    /// table's path plus its name, so a caller that cannot list the tables cannot write a tag.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetTagTables"), Description("List the tag tables of a PLC program by full path, with how many tags and user constants each holds and which one is the default. Read this before creating a tag: a tag's path is its table's path plus its name, and the default table is where to put one when nothing says otherwise.")]
        public static ResponseNetworkTopology GetTagTables(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var tables = Portal.GetTagTables(softwarePath);

                var lines = tables
                    .Select(table => $"{table.Path} | {table.TagCount} tag(s) | {table.UserConstantCount} constant(s) | {(table.IsDefault ? "default" : "")}")
                    .ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = tables.Count == 0
                        ? $"'{softwarePath}' has no tag table"
                        : $"{tables.Count} tag table(s), {tables.Sum(table => table.TagCount)} tag(s) in total",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the tag tables of '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "GetTags"), Description("List what one tag table holds: its tags with their addresses and its user constants with their values, as 'name | kind | type | address or value'. System constants are not listed, because they come from the hardware configuration and nothing can author them.")]
        public static ResponseNetworkTopology GetTags(
            [Description("softwarePath: full path to the plc software, e.g. 'PLC_0'")] string softwarePath,
            [Description("tablePath: full path of the tag table, as GetTagTables prints it")] string tablePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var entries = Portal.GetTags(softwarePath, tablePath);

                var lines = entries
                    .Select(entry => $"{entry.Name} | {entry.Kind} | {entry.DataTypeName} | {entry.Assignment}")
                    .ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = entries.Count == 0
                        ? $"'{tablePath}' is empty"
                        : $"{entries.Count} name(s) in '{tablePath}', {entries.Count(entry => entry.IsConstant)} of them constants",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the tags of '{tablePath}'");
            }
        }
    }
}
