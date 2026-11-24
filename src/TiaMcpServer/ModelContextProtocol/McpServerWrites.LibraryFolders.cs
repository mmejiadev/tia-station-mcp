using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>The project library's tree: type folders, and deleting master copies and their folders.</remarks>
    public static partial class McpServer
    {
        private const string MasterCopyDeletionNote = "A write policy names this 'master-copy-delete/<path>', apart from 'library', which only lets master copies be added. The project must be saved first (SaveProject): it is archived to the backup registry before anything is touched, as a master copy has no other copy, and TIA Portal archives only a saved project.";

        [McpServerTool(Name = "CreateLibraryTypeFolder"), Description("Create a type folder in the open project's library, e.g. 'Motors' or 'Motors/Pumps'; the folders above it must exist. A folder already there is refused. GetLibraryTypes lists every type folder, empty ones too. Openness cannot move a type between folders. The library's types and folders before are kept in the backup registry. Save the project to keep it.")]
        public static ResponseLibraryTypes CreateLibraryTypeFolder(
            [Description("folderPath: the new folder, e.g. 'Motors/Pumps'")] string folderPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var backupDirectory = Backups.Allocate("CreateLibraryTypeFolder", ChangeTarget.ProjectLibrary);
                var request = new Governance.ChangeRequest("CreateLibraryTypeFolder", ChangeTarget.ProjectLibrary, $"type folder {folderPath}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreateLibraryTypeFolder(folderPath, backupDirectory), $"Type folder '{folderPath}' created. Save the project to keep it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating type folder '{folderPath}'");
            }
        }

        [McpServerTool(Name = "DeleteMasterCopy"), Description("Delete a master copy from the open project's library, by its path as GetMasterCopies gives it, e.g. 'Motors/FB_Motor'. " + MasterCopyDeletionNote)]
        public static ResponseMasterCopies DeleteMasterCopy(
            [Description("masterCopyPath: the master copy, e.g. 'Motors/FB_Motor'")] string masterCopyPath)
        {
            return RunMasterCopyDeletion("DeleteMasterCopy", masterCopyPath, backup => Portal.DeleteMasterCopy(masterCopyPath, backup));
        }

        [McpServerTool(Name = "DeleteMasterCopyFolder"), Description("Delete an empty master copy folder from the open project's library, e.g. 'Motors/Pumps'. A folder holding anything is refused, as TIA Portal deletes it with everything in it without a word: delete what is inside first, each by its path. " + MasterCopyDeletionNote)]
        public static ResponseMasterCopies DeleteMasterCopyFolder(
            [Description("folderPath: the folder, e.g. 'Motors/Pumps'")] string folderPath)
        {
            return RunMasterCopyDeletion("DeleteMasterCopyFolder", folderPath, backup => Portal.DeleteMasterCopyFolder(folderPath, backup));
        }

        private static ResponseMasterCopies RunMasterCopyDeletion(string tool, string path, Func<string, JsonObject> apply)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.MasterCopyDeletion(path);
                var backupDirectory = Backups.Allocate(tool, target);
                var request = new Governance.ChangeRequest(tool, target, $"delete {path}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeMasterCopyChange(apply(backupDirectory), $"'{path}' was deleted from the project library. Save the project to keep it."), () => new ResponseMasterCopies(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"deleting '{path}' from the project library");
            }
        }
    }
}
