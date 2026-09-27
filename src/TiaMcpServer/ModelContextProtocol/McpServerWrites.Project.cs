using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Saving and closing the open project.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "SaveProject"), Description("Save the current TIA-Portal local project/session")]
        public static ResponseSaveProject SaveProject()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("SaveProject", ChangeTarget.Project);

                return GuardedTool.Run(GuardedWrites, request, SaveOpenProject, () => new ResponseSaveProject());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SaveAsProject"), Description("Save current TIA-Portal project/session with a new name")]
        public static ResponseSaveAsProject SaveAsProject(
            [Description("newProjectPath: defines the new path where to save the project")] string newProjectPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Refused before a plan is made: asking for a copy of something that cannot be
                // copied is a mistake in the call, not a change anyone needs to approve or audit.
                if (Portal.IsLocalSession)
                {
                    throw new McpException($"Cannot save local session as '{newProjectPath}'", McpErrorCode.InvalidParams);
                }

                var request = new Governance.ChangeRequest("SaveAsProject", ChangeTarget.Project, newProjectPath);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => SaveProjectAs(newProjectPath),
                    () => new ResponseSaveAsProject());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session as '{newProjectPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CloseProject"), Description("Close the current TIA-Portal project/session")]
        public static ResponseCloseProject CloseProject()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("CloseProject", ChangeTarget.Project);

                return GuardedTool.Run(GuardedWrites, request, CloseOpenProject, () => new ResponseCloseProject());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error closing local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static ResponseSaveProject SaveOpenProject()
        {
            var isSession = Portal.IsLocalSession;
            var what = isSession ? "Local session" : "Local project";
            var saved = isSession ? Portal.SaveSession() : Portal.SaveProject();

            if (!saved)
            {
                throw new McpException($"Failed to save {what.ToLowerInvariant()}", McpErrorCode.InternalError);
            }

            return new ResponseSaveProject
            {
                Message = $"{what} saved",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }

        private static ResponseCloseProject CloseOpenProject()
        {
            // Read before closing: whether this was a session is no longer answerable afterwards.
            var isSession = Portal.IsLocalSession;
            var what = isSession ? "Local session" : "Local project";
            var closed = isSession ? Portal.CloseSession() : Portal.CloseProject();

            if (!closed)
            {
                throw new McpException($"Failed closing {what.ToLowerInvariant()}", McpErrorCode.InternalError);
            }

            return new ResponseCloseProject
            {
                Message = $"{what} closed",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }
    }
}
