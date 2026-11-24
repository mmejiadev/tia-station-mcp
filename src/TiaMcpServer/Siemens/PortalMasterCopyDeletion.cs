using Microsoft.Extensions.Logging;
using Siemens.Engineering.Library.MasterCopies;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Deleting master copies and master copy folders of the project library. Measured 2026-11-24 on TIA Portal V21:
    /// <c>MasterCopy.Delete</c> and <c>MasterCopyUserFolder.Delete</c> both work, the folder taking everything in it without a
    /// word. A master copy has no export to keep it by, so the saved project is archived to the backup first, as the library
    /// deletions do (see PortalLibraryMaintenance); and a folder is deleted only empty.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Deletes a master copy of the project library.</summary>
        /// <param name="masterCopyPath">The master copy, e.g. <c>Motors/FB_Motor</c>, as GetMasterCopies gives its path.</param>
        /// <param name="backupDirectory">Where the saved project is archived first.</param>
        /// <returns><c>{ deleted, library }</c>: the path deleted, and the library's master copies afterwards.</returns>
        /// <exception cref="PortalException">No such master copy or folder, unsaved changes, or TIA Portal refused.</exception>
        public JsonObject DeleteMasterCopy(string masterCopyPath, string backupDirectory)
        {
            _logger?.LogInformation("Deleting master copy {MasterCopy}...", masterCopyPath);

            try
            {
                var (folderSegments, name) = MasterCopyRules.ParseMasterCopyPath(masterCopyPath);
                var root = RequireMasterCopyRoot();
                var folder = RequireMasterCopyFolder(root, folderSegments);
                var copy = MasterCopyReader.Find(folder.Folder, name)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"{MasterCopyRules.FolderPlace(folder.Names)} has no master copy '{name}'. It holds: {MasterCopyRules.List(folder.Folder.MasterCopies.Select(held => held.Name))}");
                var path = MasterCopyRules.Join(folder.Names, copy.Name);

                ArchiveProjectToBackup(backupDirectory, $"master copy {path}");
                RunRefusable(copy.Delete);

                return new JsonObject { ["deleted"] = path, ["library"] = MasterCopyReader.Read(root) };
            }
            catch (Exception ex)
            {
                throw DecorateMasterCopyFailure(AsLibraryFailure(ex, "DeleteMasterCopy", PortalErrorCode.WriteFailed), "DeleteMasterCopy", string.Empty, masterCopyPath ?? string.Empty);
            }
        }

        /// <summary>Deletes an empty master copy folder of the project library.</summary>
        /// <param name="folderPath">The folder, e.g. <c>Motors/Pumps</c>.</param>
        /// <param name="backupDirectory">Where the saved project is archived first.</param>
        /// <returns><c>{ deleted, library }</c>: the folder deleted, and the library's master copies afterwards.</returns>
        /// <exception cref="PortalException">No such folder, the top folder named, the folder holds something, unsaved changes, or TIA Portal refused.</exception>
        public JsonObject DeleteMasterCopyFolder(string folderPath, string backupDirectory)
        {
            _logger?.LogInformation("Deleting master copy folder {Folder}...", folderPath);

            try
            {
                var segments = MasterCopyRules.FolderSegments(folderPath);
                if (segments.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "folderPath is required, e.g. 'Motors' or 'Motors/Pumps'; the library's top folder cannot be deleted");
                }

                var root = RequireMasterCopyRoot();
                var found = RequireMasterCopyFolder(root, segments);
                var path = MasterCopyRules.Join(found.Names, string.Empty);
                var folder = (MasterCopyUserFolder)found.Folder;
                MasterCopyRules.RequireEmptyFolder(path, folder.MasterCopies.Select(copy => copy.Name), folder.Folders.Select(nested => nested.Name));

                ArchiveProjectToBackup(backupDirectory, $"master copy folder {path}");
                RunRefusable(folder.Delete);

                return new JsonObject { ["deleted"] = path, ["library"] = MasterCopyReader.Read(root) };
            }
            catch (Exception ex)
            {
                throw DecorateMasterCopyFailure(AsLibraryFailure(ex, "DeleteMasterCopyFolder", PortalErrorCode.WriteFailed), "DeleteMasterCopyFolder", string.Empty, folderPath ?? string.Empty);
            }
        }
    }
}
