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
    /// Reading TIA Portal's version control: the workspaces and how their objects compare. Reads only; creating,
    /// exporting and synchronising are in McpServerWrites.Vci, behind the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetVciWorkspaces"), Description("List the VCI (Version Control Interface) workspaces of the open project: each one's name, the folder its files are in, and how many project objects are mapped to them. A workspace is a folder whose files mirror project objects one to one, which a Git repository can hold; branches are Git's, not TIA Portal's. Reads only.")]
        public static ResponseMessage GetVciWorkspaces()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var workspaces = Portal.GetVciWorkspaces();

                return new ResponseMessage
                {
                    Message = workspaces.Count == 0
                        ? "The project has no VCI workspace; CreateVciWorkspace makes one"
                        : $"{workspaces.Count} VCI workspace(s): {string.Join("; ", workspaces.Select(workspace => $"'{workspace.Name}' at {workspace.RootPath}, {workspace.MappedObjects} object(s) mapped"))}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["workspaces"] = new JsonArray(workspaces.Select(workspace => (JsonNode)new JsonObject
                        {
                            ["name"] = workspace.Name,
                            ["rootPath"] = workspace.RootPath,
                            ["mappedObjects"] = workspace.MappedObjects
                        }).ToArray())
                    }
                };
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, "Failed to read the VCI workspaces");
            }
        }

        [McpServerTool(Name = "GetVciStatus"), Description("Show how each object mapped to a VCI workspace compares with its file: 'file | state | which side changed'. Equal needs nothing; Unequal with ProjectObjectChanged is synchronised ProjectToWorkspace, with WorkspaceFileChanged WorkspaceToProject (SyncVciWorkspace). TIA Portal compares content, not bytes: a reformatted file stays Equal. Reads only.")]
        public static ResponseMessage GetVciStatus(
            [Description("workspaceName: the workspace, as GetVciWorkspaces names it")] string workspaceName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                return DescribeVciStates(Portal.GetVciStatus(workspaceName), $"VCI workspace '{workspaceName}'");
            }
            catch (PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read VCI workspace '{workspaceName}'");
            }
        }

        private static ResponseMessage DescribeVciStates(IReadOnlyList<VciObjectState> states, string what)
        {
            var changed = states.Count(state => state.CompareState != "Equal");

            return new ResponseMessage
            {
                Message = $"{what}: {states.Count} object(s), {changed} not equal. {string.Join("; ", states.Where(state => state.CompareState != "Equal").Select(state => state.Line))}".TrimEnd(' ', '.'),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["objects"] = new JsonArray(states.Select(state => (JsonNode)state.Line).ToArray())
                }
            };
        }
    }
}
