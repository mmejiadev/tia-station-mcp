using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;
using TiaMcpServer.Vci;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// TIA Portal's version control: making a workspace, putting a program in it, and synchronising. Part of
    /// McpServerWrites: each changes the project or writes outside it, so each goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateVciWorkspace"), Description("Create a VCI (Version Control Interface) workspace for the open project: a folder whose files will mirror project objects, ready to be a Git repository. Give a full folder path; it is created if missing. A workspace of that name already there is refused. Then ExportToVciWorkspace puts a program in it. Nothing existing changes, so no backup is taken; save the project to keep the workspace.")]
        public static ResponseMessage CreateVciWorkspace(
            [Description("workspaceName: a name for the workspace, e.g. 'Cell'")] string workspaceName,
            [Description("rootDirectory: the full path of its folder, e.g. 'C:/Repos/Cell'")] string rootDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("CreateVciWorkspace", ChangeTarget.Project, $"{workspaceName} at {rootDirectory}");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var workspace = Portal.CreateVciWorkspace(workspaceName, rootDirectory);

                        return new ResponseMessage
                        {
                            Message = $"VCI workspace '{workspace.Name}' created at {workspace.RootPath}. Save the project to keep it.",
                            Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true, ["rootPath"] = workspace.RootPath }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating VCI workspace '{workspaceName}'");
            }
        }

        [McpServerTool(Name = "ExportToVciWorkspace"), Description("Put a PLC program in a VCI workspace: every block, PLC data type and tag table becomes one SimaticML file, in folders that mirror the project (PLC_1/Blocks/<groups>, PLC_1/PLC data types, PLC_1/PLC tags), and is mapped to it. Objects already mapped are left alone, so running it again adds only what is new. A block or type that does not compile is not exported and is named in the answer: compile first. The project itself does not change.")]
        public static ResponseMessage ExportToVciWorkspace(
            [Description("workspaceName: the workspace, as GetVciWorkspaces names it")] string workspaceName,
            [Description("softwarePath: the PLC's software, e.g. 'PLC_1'")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var target = GuardedWrites.TargetToPropose(ChangeTarget.Program(softwarePath), ChangeTarget.Project);
                var request = new Governance.ChangeRequest("ExportToVciWorkspace", target, $"{softwarePath} to {workspaceName}");

                return GuardedTool.Run(GuardedWrites, request, () => DescribeVciExport(Portal.ExportToVciWorkspace(workspaceName, softwarePath), workspaceName), () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"exporting '{softwarePath}' to VCI workspace '{workspaceName}'");
            }
        }

        [McpServerTool(Name = "SyncVciWorkspace"), Description("Synchronise a VCI workspace one way: 'ProjectToWorkspace' writes the changed project objects to their files (compile first: Openness exports nothing that does not compile); 'WorkspaceToProject' imports the changed files into the project, for example after a Git checkout of another branch. Only objects changed on the side being copied are synchronised. If any object changed on the side that would be overwritten, NOTHING is synchronised and the answer names each one: the person decides which change wins. Before the project is overwritten, its programs (blocks, types and tag tables) are exported to the backup registry. Answers with every object's state afterwards.")]
        public static ResponseMessage SyncVciWorkspace(
            [Description("workspaceName: the workspace, as GetVciWorkspaces names it")] string workspaceName,
            [Description("direction: 'ProjectToWorkspace' or 'WorkspaceToProject'")] string direction)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            if (!Enum.TryParse<VciDirection>((direction ?? string.Empty).Trim(), ignoreCase: false, out var way) || !Enum.IsDefined(typeof(VciDirection), way))
            {
                throw new McpException("direction is 'ProjectToWorkspace' or 'WorkspaceToProject'", McpErrorCode.InvalidParams);
            }

            try
            {
                var backupDirectory = Backups.Allocate("SyncVciWorkspace", ChangeTarget.Project);
                var request = new Governance.ChangeRequest("SyncVciWorkspace", ChangeTarget.Project, $"{workspaceName} {way}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeVciStates(Portal.SyncVciWorkspace(workspaceName, way, backupDirectory), $"VCI workspace '{workspaceName}' synchronised {way}"), () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"synchronising VCI workspace '{workspaceName}'");
            }
        }

        private static ResponseMessage DescribeVciExport(VciExportReport report, string workspaceName)
        {
            var left = report.Inconsistent.Count == 0 ? string.Empty : $" Not exported, as they do not compile: {string.Join(", ", report.Inconsistent)}.";
            var unsupported = report.Unsupported.Count == 0 ? string.Empty : $" No SimaticML file offered for: {string.Join(", ", report.Unsupported)}.";

            return new ResponseMessage
            {
                Message = $"{report.Exported.Count} file(s) written to VCI workspace '{workspaceName}', {report.AlreadyMapped.Count} already mapped.{left}{unsupported}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["exported"] = ToJson(report.Exported),
                    ["alreadyMapped"] = ToJson(report.AlreadyMapped),
                    ["inconsistent"] = ToJson(report.Inconsistent),
                    ["unsupported"] = ToJson(report.Unsupported)
                }
            };
        }

        private static JsonArray ToJson(IReadOnlyList<string> items)
        {
            return new JsonArray(items.Select(item => (JsonNode)item).ToArray());
        }
    }
}
