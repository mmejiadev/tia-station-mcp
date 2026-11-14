using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Git in a VCI workspace's folder: making it a repository, branches, commits, a pull, and changes moved as patch
    /// files. Part of McpServerWrites: each changes the repository, so each is planned, decided by the policy under
    /// <c>git/&lt;workspace&gt;</c> and audited; a pull also reaches a server and needs <c>git-remote/&lt;workspace&gt;</c>.
    /// The project is not touched: SyncVciWorkspace carries files into it and out of it. There is no push.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GitInitWorkspace"), Description("Make a VCI workspace's folder a Git repository, first branch 'main'. A folder that is a repository already is refused. Then GitCommit records what ExportToVciWorkspace wrote.")]
        public static ResponseMessage GitInitWorkspace(
            [Description("workspaceName: the VCI workspace, as GetVciWorkspaces names it")] string workspaceName)
        {
            return RunGit("GitInitWorkspace", workspaceName, "init", () => RepositoryOf(workspaceName).Init());
        }

        [McpServerTool(Name = "GitCreateBranch"), Description("Create a branch from the current commit of a VCI workspace's repository and switch to it, e.g. to try a change without touching 'main'. Branch names take letters, digits, '.', '_', '-' and '/' (e.g. 'feature/conveyor'). Uncommitted changes come along to the new branch.")]
        public static ResponseMessage GitCreateBranch(
            [Description("workspaceName: the VCI workspace")] string workspaceName,
            [Description("branch: the new branch's name, e.g. 'feature/conveyor'")] string branch)
        {
            return RunGit("GitCreateBranch", workspaceName, $"create and switch to {branch}", () => RepositoryOf(workspaceName).CreateBranch(branch));
        }

        [McpServerTool(Name = "GitSwitchBranch"), Description("Switch a VCI workspace's repository to another branch: its files become that branch's. Refused while there are uncommitted changes, which would be lost or carried over. The PROJECT does not change yet: then SyncVciWorkspace with 'WorkspaceToProject' brings the branch's files into TIA Portal.")]
        public static ResponseMessage GitSwitchBranch(
            [Description("workspaceName: the VCI workspace")] string workspaceName,
            [Description("branch: the branch to switch to, e.g. 'main'")] string branch)
        {
            return RunGit("GitSwitchBranch", workspaceName, $"switch to {branch}", () => RepositoryOf(workspaceName).SwitchBranch(branch));
        }

        [McpServerTool(Name = "GitCommit"), Description("Commit every change in a VCI workspace's folder with a message saying what changed. Synchronise the project into the workspace first (SyncVciWorkspace 'ProjectToWorkspace') so the files hold the latest program. The commit's author is the one Git is configured with on this machine. Nothing to commit is refused.")]
        public static ResponseMessage GitCommit(
            [Description("workspaceName: the VCI workspace")] string workspaceName,
            [Description("message: what changed, e.g. 'Conveyor: stop on sensor B2'")] string message)
        {
            return RunGit("GitCommit", workspaceName, message, () => RepositoryOf(workspaceName).Commit(message));
        }

        [McpServerTool(Name = "GitPull"), Description("Bring the remote branch's new commits into a VCI workspace's repository, only when no merge is needed (fast-forward): if both sides have new commits it stops and says so. Refused while there are uncommitted changes. It contacts the remote, so the policy must also allow git-remote/<workspace>. Git never asks for a password here: one that needs it fails. Then SyncVciWorkspace with 'WorkspaceToProject' brings the new files into TIA Portal.")]
        public static ResponseMessage GitPull(
            [Description("workspaceName: the VCI workspace")] string workspaceName)
        {
            // It changes the repository and reaches a server, and a policy has to allow both.
            var target = GuardedWrites.TargetToPropose(ChangeTarget.GitRepository(workspaceName), ChangeTarget.GitRemote(workspaceName));

            return RunGitOn("GitPull", target, "pull, fast-forward only", () => RepositoryOf(workspaceName).Pull());
        }

        [McpServerTool(Name = "GitExportChanges"), Description("Export the commits made since a branch or commit as patch files, one per commit, to carry changes to another PC or person without a server, e.g. since 'main' on a feature branch. Writes files to the folder given; the repository does not change. GitImportChanges applies them on the other side.")]
        public static ResponseMessage GitExportChanges(
            [Description("workspaceName: the VCI workspace")] string workspaceName,
            [Description("since: the branch or commit the changes were made after, e.g. 'main'")] string since,
            [Description("outputDirectory: the full path of the folder the patch files go in")] string outputDirectory)
        {
            return RunGit("GitExportChanges", workspaceName, $"commits since {since} to {outputDirectory}", () =>
            {
                var files = RepositoryOf(workspaceName).ExportChanges(since, outputDirectory);

                return files.Count == 0 ? $"No commit since '{since}'" : $"{files.Count} patch file(s): {string.Join(", ", files)}";
            });
        }

        [McpServerTool(Name = "GitImportChanges"), Description("Apply the commits of a patch file, as GitExportChanges writes them, to a VCI workspace's repository. It is checked first and, if it does not apply, nothing changes. Refused while there are uncommitted changes. Then SyncVciWorkspace with 'WorkspaceToProject' brings the changes into TIA Portal.")]
        public static ResponseMessage GitImportChanges(
            [Description("workspaceName: the VCI workspace")] string workspaceName,
            [Description("patchFile: the full path of the .patch file")] string patchFile)
        {
            return RunGit("GitImportChanges", workspaceName, patchFile, () => RepositoryOf(workspaceName).ImportChanges(patchFile));
        }

        private static ResponseMessage RunGit(string tool, string workspaceName, string detail, Func<string> run)
        {
            return RunGitOn(tool, ChangeTarget.GitRepository(workspaceName), detail, run);
        }

        private static ResponseMessage RunGitOn(string tool, string target, string detail, Func<string> run)
        {
            try
            {
                var request = new Governance.ChangeRequest(tool, target, detail);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var said = run();

                        return new ResponseMessage
                        {
                            Message = said.Length == 0 ? $"{tool} done" : said,
                            Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"running {tool}");
            }
        }
    }
}
