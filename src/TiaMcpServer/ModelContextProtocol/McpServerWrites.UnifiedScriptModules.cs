using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScripts;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>The global script modules of a WinCC Unified HMI, the Scripts folder of its project tree.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetUnifiedScriptModules"), Description("Read the global script modules of a WinCC Unified HMI, the Scripts folder of its project tree: each module's name and its JavaScript as TIA Portal holds it. The HMI path is the Unified software's, e.g. 'PC-System_1/HMI_RT_1'.")]
        public static ResponseUnifiedScriptModules GetUnifiedScriptModules(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var content = Portal.GetUnifiedScriptModules(hmiPath);

                return new ResponseUnifiedScriptModules(content)
                {
                    Message = $"{Count(content, "modules")} script module(s) on '{hmiPath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the script modules of '{hmiPath}'");
            }
        }

        [McpServerTool(Name = "WriteUnifiedScriptModule"), Description("Write a global script module to a WinCC Unified HMI, the Scripts folder of its project tree. Write what screens may call as 'export function name(...) { ... }': TIA Portal exports every top-level function and moves anything else to the module's global definition area, and 'export default' and export lists are refused, as it breaks them. A module of that name is refused unless replace is true, as TIA Portal replaces it without a word. The code is checked first, as TIA Portal imports JavaScript that does not parse and compiles it with no error: a syntax error or a fault of the kind CheckUnifiedScripts finds refuses the write; its warnings come back as findings. Every module of the HMI is exported to the backup registry first. Save the project to keep it, and compile the HMI.")]
        public static ResponseUnifiedScriptModules WriteUnifiedScriptModule(
            [Description("hmiPath: full path to the WinCC Unified HMI software")] string hmiPath,
            [Description("name: the module's name: letters, digits and '_'")] string name,
            [Description("code: the module's JavaScript")] string code,
            [Description("replace: true to replace a module of that name")] bool replace = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var module = new UnifiedScriptModuleWrite((name ?? string.Empty).Trim(), code ?? string.Empty, replace);
                var target = ChangeTarget.Program(hmiPath, $"Scripts/{module.Name}");
                var backupDirectory = Backups.Allocate("WriteUnifiedScriptModule", target);
                var request = new Governance.ChangeRequest("WriteUnifiedScriptModule", target, UnifiedScriptModuleRules.Summary(module)).WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeWrittenScriptModule(Portal.WriteUnifiedScriptModule(hmiPath, module, backupDirectory)), () => new ResponseUnifiedScriptModules(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"writing script module '{name}' to '{hmiPath}'");
            }
        }

        private static ResponseUnifiedScriptModules DescribeWrittenScriptModule(JsonObject written)
        {
            return new ResponseUnifiedScriptModules(written)
            {
                Message = $"Script module '{written["name"]}' {(written["replaced"]!.GetValue<bool>() ? "replaced" : "created")}, {Count(written, "findings")} warning(s). Save the project to keep it, and compile the HMI.",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }
    }
}
