using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Creating WinCC Unified logging. Reading it is <c>GetUnifiedLogging</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateUnifiedLogging"), Description("Create WinCC Unified data logs, alarm logs and logging tags from JSON: {\"dataLogs\": [{\"name\": \"Log_Tolva\"}], \"alarmLogs\": [{\"name\": \"Alarmes\", \"classes\": [\"Alarm\", \"Warning\"]}], \"loggingTags\": [{\"tag\": \"Nivell\", \"name\": \"Nivell_log\", \"dataLog\": \"Log_Tolva\", \"mode\": \"cyclic\", \"cycle\": \"T1s\", \"cycleFactor\": 5}]}. A logging tag logs one HMI tag (CreateUnifiedTags makes one) into a data log, made in the same call or existing; its name is unique among the logging tags of that tag. Its mode is onChange (the default), cyclic or onDemand; a cyclic one needs a cycle, and only a cyclic one takes it: T500ms, T1s, T2s, T5s and T10s were taken on TIA Portal V20, T100ms and T250ms refused on an internal tag. An optional cycleFactor, a whole number from 1, writes every that many cycles. An alarm log logs the alarm classes it names, system ones (Alarm, Warning, ...) or ones CreateUnifiedAlarms made, at least one; a class already logged to a log is refused rather than moved. Data logs and alarm logs share one set of names. Creating a log is refused when a class or logging tag still names a deleted log of that name, as it would be logged there without a word; to find those, a call that creates a data log reads every HMI tag's logging tags, which takes a while on an HMI with thousands of tags. The HMI tag is found by its name as TIA Portal spells it. Everything is checked first, and the call is all or nothing. Nothing existing changes except the classes' log, set only from empty and saved to the backup registry first. Answers with what was created, read back; compile the HMI to check it.")]
        public static ResponseUnifiedLogging CreateUnifiedLogging(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("logging: the logs and logging tags as JSON, see the tool description")] string logging)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a
                // change anyone needs to approve or audit.
                var list = UnifiedLoggingListReader.Parse(logging);
                // The backup holds the classes' logs before they change; the plan names every log, class and logging tag.
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("CreateUnifiedLogging", target);
                var request = new Governance.ChangeRequest("CreateUnifiedLogging", target, UnifiedLoggingSummary.Of(list)).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeCreatedLogging(Portal.CreateUnifiedLogging(hmiPath, list, backupDirectory), hmiPath),
                    () => new ResponseUnifiedLogging(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating Unified logging on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedLogging DescribeCreatedLogging(JsonObject created, string hmiPath)
        {
            var logs = Count(created, "dataLogs") + Count(created, "alarmLogs");
            var logged = Count(created, "loggingTags");

            return new ResponseUnifiedLogging(created)
            {
                Message = $"{logs} log(s) and {logged} logging tag(s) created on '{hmiPath}'. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["logs"] = logs,
                    ["loggingTags"] = logged
                }
            };
        }
    }
}
