using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTextLists;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Writing a WinCC Unified text list. Reading them is <c>GetUnifiedTextLists</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateUnifiedTextList"), Description("Create a text list on a WinCC Unified HMI, or replace the one of that name: one text per value of a tag - 'Stopped' for 0, 'Running' for 1, 'Fault' for 2. JSON: {\"name\": \"States\", \"entries\": [{\"value\": 0, \"text\": \"Stopped\"}, {\"value\": 1, \"text\": \"Running\"}]}. A name is letters, digits and underscores; a value is a whole number, once per list; a text is one line in the project's editing language. Ranges of values are not offered: TIA Portal V20 imports them as an empty list. Every list is exported to the backup registry first, and the list is read back after it is written: when TIA Portal did not keep it as asked, it is undone and the call fails saying what came back. Show it with a symbolicIOField whose properties give \"ResourceList\": \"States\" and whose tags give ProcessValue, or with a 'list' animation of DrawUnifiedScreen: {\"property\": \"Text\", \"tag\": \"State\", \"list\": \"States\"}. Answers with the list read back.")]
        public static ResponseUnifiedTextLists CreateUnifiedTextList(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'HMI_MTP700/HMI_RT_1'")] string hmiPath,
            [Description("textList: the list as JSON, see the tool description")] string textList)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a list that does not parse is a mistake in the call, not a
                // change anyone needs to approve or audit.
                var list = UnifiedTextListReader.Parse(textList);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("CreateUnifiedTextList", target);
                var request = new Governance.ChangeRequest("CreateUnifiedTextList", target, list.Name).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeWrittenTextList(Portal.CreateUnifiedTextList(hmiPath, list, backupDirectory), hmiPath),
                    () => new ResponseUnifiedTextLists(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"writing a Unified text list on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedTextLists DescribeWrittenTextList(JsonObject written, string hmiPath)
        {
            var entries = (written["entries"] as JsonArray)?.Count ?? 0;

            return new ResponseUnifiedTextLists(written)
            {
                Message = $"Text list '{written["name"]}' written to '{hmiPath}' with {entries} entr{(entries == 1 ? "y" : "ies")}, read back as asked. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["entries"] = entries
                }
            };
        }
    }
}
