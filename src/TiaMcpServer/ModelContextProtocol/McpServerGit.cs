using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Git;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading the Git repository in a VCI workspace's folder: its status and history. Reads only; branching,
    /// committing, pulling and patches are in McpServerWrites.Git, behind the guard. Git works in a workspace's folder
    /// and nowhere else: the folder is looked up from the workspace's name, never taken from the caller.
    /// </remarks>
    public static partial class McpServer
    {
        private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

        [McpServerTool(Name = "GitStatus"), Description("Show the Git state of a VCI workspace's folder: the current branch (and how far it is ahead of or behind its remote), every local branch with the current one marked '*', and every changed file as 'git status --porcelain' lists it. Reads only. GitInitWorkspace makes the folder a repository.")]
        public static ResponseMessage GitStatus(
            [Description("workspaceName: the VCI workspace, as GetVciWorkspaces names it")] string workspaceName)
        {
            try
            {
                var repository = RepositoryOf(workspaceName);
                var status = repository.Status();
                var branches = repository.Branches();

                return new ResponseMessage
                {
                    Message = $"{status[0]}; {status.Count - 1} changed file(s); branches: {string.Join(", ", branches)}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["branch"] = status[0],
                        ["changed"] = ToJson(status.Skip(1).ToList()),
                        ["branches"] = ToJson(branches)
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the Git status of workspace '{workspaceName}'");
            }
        }

        [McpServerTool(Name = "GitLog"), Description("List the latest commits of the current branch in a VCI workspace's folder: 'hash, date, author, message'. Reads only.")]
        public static ResponseMessage GitLog(
            [Description("workspaceName: the VCI workspace, as GetVciWorkspaces names it")] string workspaceName,
            [Description("count: how many commits, 1 to 200 (default 20)")] int count = 20)
        {
            try
            {
                var log = RepositoryOf(workspaceName).Log(count);

                return new ResponseMessage
                {
                    Message = log.Count == 0 ? "No commit yet" : string.Join("\n", log),
                    Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true, ["commits"] = ToJson(log) }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the Git history of workspace '{workspaceName}'");
            }
        }

        /// <remarks>The workspace's folder is read from TIA Portal under the Openness gate, and Git is run outside it.</remarks>
        private static GitRepository RepositoryOf(string workspaceName)
        {
            string root;

            using (OpennessGate.Enter())
            {
                var name = (workspaceName ?? string.Empty).Trim();
                root = Portal.GetVciWorkspaces().FirstOrDefault(workspace => workspace.Name == name)?.RootPath
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"No VCI workspace called '{name}'; GetVciWorkspaces lists them");
            }

            return new GitRepository(Git.GitInput.RequireWorkspaceFolder(root), new GitRunner(GitTimeout));
        }
    }
}
