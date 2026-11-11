using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Archiving the project. Part of McpServerWrites: it writes a file outside the project, so it
    /// goes through the guard although nothing existing changes.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ArchiveProject"), Description("Archive the open project into one compressed .zapNN file, as TIA Portal's Project > Archive does: how a project is handed in or moved to another machine. Give the name without an extension: the server adds the one of this TIA Portal's version (.zap21), as TIA Portal V21 writes exactly the name it is given. A file of that name already there is refused, so nothing is overwritten. Save the project first with SaveProject: whether unsaved changes go into the archive has not been measured. RetrieveProject opens the archive again.")]
        public static ResponseMessage ArchiveProject(
            [Description("targetDirectory: the folder to write the archive to, e.g. 'C:/Class/Handed in'")] string targetDirectory,
            [Description("archiveName: the archive's name without extension, e.g. 'Motor_Samreen'")] string archiveName,
            [Description("discardRestorableData: true leaves out what TIA Portal rebuilds on retrieving, for a smaller file; false keeps everything (default)")] bool discardRestorableData = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("ArchiveProject", ChangeTarget.Project, $"{archiveName} in {targetDirectory}");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeWrittenFile("Project archived to", "archivePath", Portal.ArchiveProject(targetDirectory, archiveName, discardRestorableData), null),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"archiving the project as '{archiveName}' in '{targetDirectory}'");
            }
        }

        /// <remarks>Shared with UpgradeGlobalLibrary: both answer with one file written outside the project.</remarks>
        private static ResponseMessage DescribeWrittenFile(string what, string key, string path, string? warning)
        {
            var meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = true,
                [key] = path
            };

            if (warning != null)
            {
                meta["warning"] = warning;
            }

            return new ResponseMessage
            {
                Message = warning == null ? $"{what} '{path}'" : $"{what} '{path}'. {warning}",
                Meta = meta
            };
        }
    }
}
