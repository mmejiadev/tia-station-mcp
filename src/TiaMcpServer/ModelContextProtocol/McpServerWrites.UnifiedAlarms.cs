using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedAlarms;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Creating WinCC Unified alarms. Reading them is <c>GetUnifiedAlarms</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateUnifiedAlarms"), Description("Create WinCC Unified alarm classes, discrete alarms and analog alarms from JSON: {\"classes\": [{\"name\": \"Tolva\", \"priority\": 10, \"stateMachine\": \"raiseClearRequiresAcknowledgement\", \"states\": {\"raised\": {\"backColor\": \"255, 128, 0\", \"textColor\": \"black\", \"flashing\": true}}}], \"discrete\": [{\"name\": \"Tolva_Plena\", \"tag\": \"Alarms\", \"bit\": 3, \"trigger\": \"risingEdge\", \"class\": \"Tolva\", \"priority\": 3, \"text\": \"Tolva plena\", \"info\": \"Buidar la tolva\", \"origin\": \"Tolva\", \"area\": \"Linia 1\"}], \"analog\": [{\"name\": \"Pressio_Alta\", \"tag\": \"Pressure\", \"condition\": \"upperLimit\", \"value\": 8.5, \"class\": \"Warning\", \"text\": \"Pressio alta\"}]}. A discrete alarm is raised by one bit of a Bool, Byte, Word, DWord or LWord HMI tag (bit 0 by default, rising edge by default); an analog alarm compares a number tag (any integer type, Real or LReal) with a value: upperLimit, upperLimitOrEqual, lowerLimit, lowerLimitOrEqual, equal or notEqual. The tag is an HMI tag, internal or linked to the PLC; CreateUnifiedTags makes one. Every alarm needs a text; class defaults to Alarm, and may be a system class (Alarm, Warning, Information, Critical and the rest GetUnifiedAlarms lists) or one created in the same call. Priorities run from 0 to 16. A class's states are raised, acknowledged, cleared and acknowledgedCleared; its stateMachine is raise, raiseClear, raiseRequiresAcknowledgement, raiseClearOptionalAcknowledgement, raiseClearRequiresAcknowledgement or raiseClearRequiresAcknowledgementAndReset. Everything is checked first, as the compile would refuse it, and the call is all or nothing: a taken name, a missing tag or class, a tag of the wrong type for its alarm, a bit past its tag, a tag and bit already raising an alarm, or a value outside its tag's type leaves nothing created. Nothing existing changes, so no backup is taken. Answers with what was created, read back.")]
        public static ResponseUnifiedAlarms CreateUnifiedAlarms(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("alarms: the alarm list as JSON, see the tool description")] string alarms)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a
                // change anyone needs to approve or audit.
                var list = UnifiedAlarmListReader.Parse(alarms);
                // No backup is taken, so the plan names every class and alarm: it is the only record of the change.
                var request = new Governance.ChangeRequest("CreateUnifiedAlarms", ChangeTarget.Program(hmiPath), UnifiedAlarmSummary.Of(list));

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeCreatedAlarms(Portal.CreateUnifiedAlarms(hmiPath, list), hmiPath),
                    () => new ResponseUnifiedAlarms(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating Unified alarms on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedAlarms DescribeCreatedAlarms(JsonObject created, string hmiPath)
        {
            var classes = created["classes"]!.AsArray().Count;
            var alarms = created["discrete"]!.AsArray().Count + created["analog"]!.AsArray().Count;

            return new ResponseUnifiedAlarms(created)
            {
                Message = $"{classes} alarm class(es) and {alarms} alarm(s) created on '{hmiPath}'. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["classes"] = classes,
                    ["alarms"] = alarms
                }
            };
        }
    }
}
