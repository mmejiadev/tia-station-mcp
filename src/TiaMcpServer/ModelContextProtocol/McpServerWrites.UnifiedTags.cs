using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Creating WinCC Unified HMI tags. Reading them is <c>GetUnifiedTags</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateUnifiedTags"), Description("Create WinCC Unified HMI tags, and the tag tables they go in, from JSON: {\"newTables\": [\"Motors\"], \"tags\": [{\"name\": \"Speed_SP\", \"table\": \"Motors\", \"dataType\": \"Int\"}, {\"name\": \"Motor_Run\", \"connection\": \"HMI_Connection_1\", \"plcTag\": \"Motor_Run\"}]}. An internal tag gives a dataType: Bool, Byte, SInt, USInt, Int, UInt, DInt, UDInt, LInt, ULInt, Real, LReal, Word, DWord, LWord, WChar, WString, DateTime or LTime. A linked tag gives an integrated connection and the PLC tag's name, and TIA Portal fills in its data type and address; GetUnifiedTags lists the connections. Openness cannot create an integrated connection: when the HMI has none, make it once in TIA Portal (Devices & networks, Connections). Without 'table' a tag goes in the default tag table. Names and table names take up to 128 characters. Everything is checked first and the call is all or nothing: a taken name, a missing table or connection, or a tag TIA Portal refuses leaves nothing created. Nothing existing changes, so no backup is taken. Answers with the tags read back.")]
        public static ResponseUnifiedTags CreateUnifiedTags(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'HMI_MTP700/HMI_RT_1'")] string hmiPath,
            [Description("tags: the tag list as JSON, see the tool description")] string tags)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a
                // change anyone needs to approve or audit.
                var list = UnifiedTagListReader.Parse(tags);
                // No backup is taken, so the plan names every tag and link: it is the only record of the change.
                var detail = string.Join(", ", list.Tags.Select(tag => tag.Link == null ? $"{tag.Name} ({tag.DataType})" : $"{tag.Name} -> {tag.Link.Connection}/{tag.Link.PlcTag}"));
                var request = new Governance.ChangeRequest("CreateUnifiedTags", ChangeTarget.Program(hmiPath), detail);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeCreatedTags(Portal.CreateUnifiedTags(hmiPath, list), hmiPath),
                    () => new ResponseUnifiedTags(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating Unified tags on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedTags DescribeCreatedTags(JsonArray created, string hmiPath)
        {
            return new ResponseUnifiedTags(new JsonObject { ["tags"] = created })
            {
                Message = $"{created.Count} tag(s) created on '{hmiPath}'. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["tags"] = created.Count
                }
            };
        }
    }
}
