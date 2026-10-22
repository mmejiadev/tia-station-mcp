using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.History;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Creating a project, adding stations to it, saving and closing it.
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

        [McpServerTool(Name = "CreateProject"), Description("Create an empty TIA Portal project and open it. TIA Portal puts it in a folder named after the project inside targetDirectory; a folder that already exists is refused, so nothing is overwritten. Refused while a project is open, because creating one would close it without saving: SaveProject and CloseProject first. Then add stations with AddDevice.")]
        public static ResponseMessage CreateProject(
            [Description("targetDirectory: the directory the project folder is created in, e.g. 'C:/Projects'")] string targetDirectory,
            [Description("projectName: the project's name, which is also its folder's name")] string projectName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("CreateProject", ChangeTarget.Project, $"{projectName} in {targetDirectory}");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => CreateEmptyProject(targetDirectory, projectName),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating project '{projectName}' in '{targetDirectory}'");
            }
        }

        /// <remarks>
        /// Checked here and not in Portal.CreateProject, which closes whatever is open on purpose:
        /// the test bench relies on that. A caller over MCP may have hours of unsaved work in the
        /// open project, and Openness closes it without asking.
        /// </remarks>
        private static ResponseMessage CreateEmptyProject(string targetDirectory, string projectName)
        {
            if (!string.IsNullOrEmpty(Portal.OpenProjectPath))
            {
                throw new McpException(
                    $"'{Portal.OpenProjectPath}' is open, and creating a project would close it without saving. Save and close it first.",
                    McpErrorCode.InvalidParams);
            }

            var projectPath = Portal.CreateProject(targetDirectory, projectName);

            return new ResponseMessage
            {
                Message = RecordOpenProject(ProjectEvent.Created, $"Project '{projectName}' created and open at '{projectPath}'"),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["projectPath"] = projectPath
                }
            };
        }

        [McpServerTool(Name = "AddDevice"), Description("Add a station to the open project: a CPU, an HMI panel, anything TIA Portal creates from an order number. The answer gives the path of the device's software - a PLC program or an HMI target - to pass to the other tools; a panel's HMI target sits on an item TIA Portal names itself, e.g. 'HMI_1/HMI_RT_2'. A name another device already has is refused. Nothing existing changes, so no backup is taken. Then PlugModule, SetDeviceAddress and CompileHardware.")]
        public static ResponseMessage AddDevice(
            [Description("typeIdentifier: what to create, as Openness names it, e.g. 'OrderNumber:6ES7 511-1AL03-0AB0/V4.0'")] string typeIdentifier,
            [Description("deviceName: name for the station, e.g. 'PLC_1'")] string deviceName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("AddDevice", ChangeTarget.Program(deviceName), typeIdentifier);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeAddedStation(Portal.AddDevice(typeIdentifier, deviceName), typeIdentifier),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"adding device '{deviceName}' from '{typeIdentifier}'");
            }
        }

        [McpServerTool(Name = "AddPcStation"), Description("Add a PC station carrying an HMI runtime. Only the two runtimes measured are accepted: WinCC Unified PC RT V20 ('OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2') and WinCC RT Advanced V17 ('OrderNumber:6AV2 104-0xxxx-xxxx/17.0.0.0'). A PC station is a rack with the runtime plugged into it, which AddDevice cannot build. TIA Portal names the runtime itself (e.g. 'HMI_RT_1'); the answer gives the software path to pass to the HMI tools. Other PC software is refused. Nothing existing changes, so no backup is taken.")]
        public static ResponseMessage AddPcStation(
            [Description("stationName: name for the PC station, e.g. 'PC-System_1'")] string stationName,
            [Description("runtimeTypeIdentifier: the HMI runtime, e.g. 'OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2'")] string runtimeTypeIdentifier)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("AddPcStation", ChangeTarget.Program(stationName), runtimeTypeIdentifier);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeAddedStation(Portal.AddPcStation(stationName, runtimeTypeIdentifier), runtimeTypeIdentifier),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"adding PC station '{stationName}' with '{runtimeTypeIdentifier}'");
            }
        }

        private static ResponseMessage DescribeAddedStation(string added, string typeIdentifier)
        {
            var softwarePaths = string.Join(", ", Portal.GetSoftwarePaths(added));
            var software = softwarePaths.Length == 0 ? string.Empty : $"; its software is at {softwarePaths}";

            return new ResponseMessage
            {
                Message = $"Device '{added}' added from '{typeIdentifier}'{software}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["deviceName"] = added,
                    ["softwarePaths"] = softwarePaths
                }
            };
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
