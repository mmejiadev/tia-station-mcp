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
    /// Upgrading a project from an older TIA Portal. Part of McpServerWrites: an upgrade writes a new
    /// project to disk, so it goes through the guard even though the original is left as it was.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "OpenProjectWithUpgrade"), Description("Open a project made with an older TIA Portal (.ap20 on V21) as an upgraded copy, and leave the copy open. The original is not changed: TIA Portal writes the copy to a new folder beside the project's own, named after that folder with '_V21' added, and a '.backup' folder next to it. A folder that already exists is refused, so nothing is overwritten. Refused while a project is open, because that would close it without saving: SaveProject and CloseProject first. A project of this TIA Portal's version is refused too: use OpenProject. The upgrade can take a couple of minutes. The answer gives the new project's path.")]
        public static ResponseRetrieveProject OpenProjectWithUpgrade(
            [Description("path: full path of the project file, e.g. 'C:/Class/Motor/Motor.ap20'")] string path)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("OpenProjectWithUpgrade", ChangeTarget.Project, path);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeUpgrade(ProjectEvent.Opened, path, () => Portal.OpenProjectWithUpgrade(path)),
                    () => new ResponseRetrieveProject(string.Empty));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"upgrading project '{path}'");
            }
        }

        [McpServerTool(Name = "RetrieveProjectWithUpgrade"), Description("Retrieve an archive made with an older TIA Portal (.zap20 on V21) into a directory, upgrade it and leave it open. The archive is not changed. The directory must be empty or new, so nothing is overwritten; it ends up holding the project as extracted, the upgraded copy in a folder ending in '_V21' and a '.backup' folder, each named after the project inside the archive. Refused while a project is open, because that would close it without saving: SaveProject and CloseProject first. An archive of this TIA Portal's version is refused too: use RetrieveProject. The upgrade can take a couple of minutes. The answer gives the new project's path.")]
        public static ResponseRetrieveProject RetrieveProjectWithUpgrade(
            [Description("archivePath: full path of the .zapNN archive")] string archivePath,
            [Description("targetDirectory: an empty or new directory to extract into, e.g. 'C:/Class/Upgraded'")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("RetrieveProjectWithUpgrade", ChangeTarget.Project, $"{archivePath} into {targetDirectory}");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeUpgrade(ProjectEvent.Retrieved, archivePath, () => Portal.RetrieveProjectWithUpgrade(archivePath, targetDirectory)),
                    () => new ResponseRetrieveProject(string.Empty));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"upgrading archive '{archivePath}' into '{targetDirectory}'");
            }
        }

        /// <remarks>
        /// Refused here and not in the portal layer for the reason <see cref="CreateEmptyProject"/>
        /// gives: over MCP the open project may hold hours of unsaved work.
        /// </remarks>
        private static ResponseRetrieveProject DescribeUpgrade(ProjectEvent projectEvent, string source, Func<string> upgrade)
        {
            if (!string.IsNullOrEmpty(Portal.OpenProjectPath))
            {
                throw new McpException(
                    $"'{Portal.OpenProjectPath}' is open, and upgrading a project would close it without saving. Save and close it first.",
                    McpErrorCode.InvalidParams);
            }

            var projectPath = upgrade();

            return new ResponseRetrieveProject(projectPath)
            {
                Message = RecordOpenProject(projectEvent, $"'{source}' upgraded and open at '{projectPath}'; the original was not changed"),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["projectPath"] = projectPath
                }
            };
        }
    }
}
