using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Changing WinCC Unified log settings. Reading them is <c>GetUnifiedLogging</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "SetUnifiedLogSettings"), Description("Change the settings of existing WinCC Unified data logs and alarm logs, from JSON: {\"logs\": [{\"name\": \"Log_Tolva\", \"maxSize\": 500, \"duration\": {\"days\": 30}, \"segment\": {\"maxSize\": 50, \"duration\": {\"hours\": 12}, \"start\": \"2026-01-01T00:00:00\"}, \"backupPath\": \"D:\\\\Backup\"}]}. Only what is given changes. maxSize is the number TIA Portal shows (1000 on a new log, and 100 for a segment); a period is given whole as days, hours (0-23), minutes and seconds (0-59), a part left out being 0 (a new log keeps 7 days, a segment 1 day). A segment can be neither larger nor longer than its log; \"backupPath\": \"\" turns the backup off, a path turns it on. The storage medium is left as it is: on a PC only Default works, as Openness V20 cannot set the folder Local needs. The writes are ordered so TIA Portal, which refuses each single write that leaves a segment above its log, takes them. Everything is checked first; all or nothing; each log's settings before are kept in the backup registry. Answers with the settings read back.")]
        public static ResponseUnifiedLogging SetUnifiedLogSettings(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("settings: the logs and their settings as JSON, see the tool description")] string settings)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a change to approve.
                var requests = UnifiedLogSettingsListReader.Parse(settings);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("SetUnifiedLogSettings", target);
                var request = new Governance.ChangeRequest("SetUnifiedLogSettings", target, UnifiedLogSettingsSummary.Of(requests)).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeLogSettings(Portal.SetUnifiedLogSettings(hmiPath, requests, backupDirectory), hmiPath),
                    () => new ResponseUnifiedLogging(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"setting Unified log settings on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedLogging DescribeLogSettings(JsonObject changed, string hmiPath)
        {
            var count = Count(changed, "logs");

            return new ResponseUnifiedLogging(changed)
            {
                Message = $"The settings of {count} log(s) on '{hmiPath}' changed. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["logs"] = count
                }
            };
        }
    }
}
