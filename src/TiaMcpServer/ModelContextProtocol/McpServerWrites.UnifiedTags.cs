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
        [McpServerTool(Name = "CreateUnifiedTags"), Description("Create WinCC Unified HMI tags with their settings, and the tag tables and groups they go in, from JSON: {\"newTables\": [\"Motors\", \"Plant/Area_1/Valves\"], \"tags\": [{\"name\": \"Speed_SP\", \"table\": \"Motors\", \"dataType\": \"Int\", \"initialValue\": 0, \"limits\": {\"lower\": 0, \"upper\": 1500}, \"comment\": \"Set point\"}, {\"name\": \"Levels\", \"dataType\": \"Array [0..3] of Real\"}, {\"name\": \"Motor_Run\", \"connection\": \"HMI_Connection_1\", \"plcTag\": \"Motor_Run\"}, {\"name\": \"Pressure\", \"connection\": \"HMI_Connection_1\", \"plcTag\": \"AI_Pressure\", \"acquisition\": {\"mode\": \"cyclicOnUse\", \"cycle\": \"T500ms\"}, \"scaling\": {\"plc\": [0, 27648], \"hmi\": [0, 10]}, \"substitute\": {\"usage\": \"invalidValue\", \"value\": 0}}]}. An internal tag gives a dataType: Bool, Byte, SInt, USInt, Int, UInt, DInt, UDInt, LInt, ULInt, Real, LReal, Word, DWord, LWord, WChar, WString, DateTime or LTime, or Array [0..n] of one of them (one dimension, from 0). A linked tag gives an integrated connection and the PLC tag's name, and TIA Portal fills in its data type and address; a PLC array, UDT, element or member links the same way ('DB.Array', 'DB.Array[3].Member'). GetUnifiedTags lists the connections. Settings: comment and limits on any tag (limits only on numbers; a number is a constant, a string names the HMI tag holding the limit); initialValue on any tag (0 or 1 for a Bool, a number with a point for a Real); persistent and maxLength (WString) only on internal tags; acquisition (cyclicOnUse with a cycle T100ms, T250ms, T500ms, T1s, T2s, T5s or T10s, or onDemand with none), scaling (PLC and HMI ranges whose start and end differ) and substitute (usage invalidValue, rangeViolation or invalidValueOrRangeViolation, and a value) only on linked tags. Values are checked against the tag's type, the way the compile would refuse them. Thresholds cannot be created: Openness V20 does not support it. A new table is a name or a 'Group/Subgroup/Table' path whose missing groups are created; table names are unique on the whole HMI. Openness cannot create an integrated connection: when the HMI has none, make it once in TIA Portal (Devices & networks, Connections). Without 'table' a tag goes in the default tag table. Names and table names take up to 128 characters. Everything is checked first and the call is all or nothing: a taken name, a system tag's name, a missing table, connection or limit tag, a value that does not fit, or a tag TIA Portal refuses leaves nothing created. Nothing existing changes, so no backup is taken. Answers with the tags read back.")]
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
                var detail = string.Join(", ", list.Tags.Select(UnifiedTagSummary.Of));
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
