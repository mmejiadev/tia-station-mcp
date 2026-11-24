using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>A global library past its first publish: new versions of its types, and the library archived and retrieved.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "UpdateGlobalLibraryTypes"), Description("Bring the new released versions of project library types to a global library file that already holds them, e.g. version 0.0.2 of a block type published at 0.0.1, so other projects can take it; the newest becomes the global library's default. A version in work is not brought (ReleaseLibraryType first). Refused when the global library lacks the type (PublishLibraryTypes publishes it the first time), already holds every released version, or holds a version the project library never had - the two went different ways; and when a new version depends on a type version the global library does not hold and the call does not name. A library open in TIA Portal is refused. A write policy names the library by its full folder, e.g. 'global-library/C:/Libraries/Motors'. The library is archived to the backup registry first, then saved and closed.")]
        public static ResponseLibraryTypes UpdateGlobalLibraryTypes(
            [Description("globalLibraryPath: the global library file, e.g. 'C:\\Libraries\\Motors\\Motors.al21'")] string globalLibraryPath,
            [Description("typeNames: the types, exactly as GetLibraryTypes names them")] string[] typeNames)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var names = (typeNames ?? Array.Empty<string>()).Select(name => (name ?? string.Empty).Trim()).ToList();
                var target = ChangeTarget.GlobalLibraryFile(globalLibraryPath);
                var backupDirectory = Backups.Allocate("UpdateGlobalLibraryTypes", target);
                var request = new Governance.ChangeRequest("UpdateGlobalLibraryTypes", target, $"{globalLibraryPath}: new versions of {string.Join(", ", names)}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.UpdateGlobalLibraryTypes(globalLibraryPath!, names, backupDirectory), $"New versions of {names.Count} type(s) brought to '{globalLibraryPath}'."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"bringing new versions to '{globalLibraryPath}'");
            }
        }

        [McpServerTool(Name = "ArchiveGlobalLibrary"), Description("Archive a global library into one compressed file (.zal21), as TIA Portal's Archive does: how a library is handed in or moved. The library is opened read-only and not changed; one open in TIA Portal with unsaved changes is refused by TIA Portal. An archive of that name already in the folder is refused, so nothing is overwritten. RetrieveGlobalLibrary unpacks it again. A write policy names the library by its full folder, e.g. 'global-library/C:/Libraries/Motors'.")]
        public static ResponseLibraryTypes ArchiveGlobalLibrary(
            [Description("globalLibraryPath: the global library file, e.g. 'C:\\Libraries\\Motors\\Motors.al21'")] string globalLibraryPath,
            [Description("targetDirectory: the folder to write the archive to, e.g. 'C:\\Backups'")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("ArchiveGlobalLibrary", ChangeTarget.GlobalLibraryFile(globalLibraryPath), $"archive to {targetDirectory}");

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.ArchiveGlobalLibrary(globalLibraryPath, targetDirectory), $"'{globalLibraryPath}' archived to '{targetDirectory}'."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"archiving '{globalLibraryPath}'");
            }
        }

        [McpServerTool(Name = "RetrieveGlobalLibrary"), Description("Retrieve an archived global library (.zal21) into an empty or new folder, where it makes a folder of its own, and leave it closed. A folder holding anything is refused, as TIA Portal cancels the retrieve there without saying why. Answers with the retrieved library's .al21 file, which GetLibraryTypes, CopyLibraryTypes and the other library tools take, and its types. A write policy names the folder retrieved into, e.g. 'global-library/C:/Libraries/Restored'.")]
        public static ResponseLibraryTypes RetrieveGlobalLibrary(
            [Description("archivePath: the archive, e.g. 'C:\\Backups\\Motors.zal21'")] string archivePath,
            [Description("targetDirectory: an empty or new folder, e.g. 'C:\\Libraries\\Restored'")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("RetrieveGlobalLibrary", ChangeTarget.GlobalLibraryFolder(targetDirectory), $"retrieve {archivePath}");

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.RetrieveGlobalLibrary(archivePath, targetDirectory), $"'{archivePath}' retrieved into '{targetDirectory}'."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"retrieving '{archivePath}'");
            }
        }
    }
}
