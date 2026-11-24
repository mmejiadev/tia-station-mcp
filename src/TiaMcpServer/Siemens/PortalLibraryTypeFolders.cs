using Microsoft.Extensions.Logging;
using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The folder tree of a library's types. Measured 2026-11-24 on TIA Portal V21: <c>LibraryTypeUserFolderComposition.Create</c>
    /// makes a folder, nested ones too. Openness has no call that moves a type between folders, so a folder is where new types
    /// are put by hand, and where TIA Portal keeps those it copies.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Creates a type folder in the project library.</summary>
        /// <param name="folderPath">The new folder, e.g. <c>Motors/Pumps</c>; the folders above it must exist.</param>
        /// <param name="backupDirectory">Where the project library's types and folders before are written.</param>
        /// <returns>The project library's folders and types, read back.</returns>
        /// <exception cref="PortalException">The path is empty or not one, a folder above it is missing, or it is there already.</exception>
        public JsonObject CreateLibraryTypeFolder(string folderPath, string backupDirectory)
        {
            _logger?.LogInformation("Creating library type folder {Folder}...", folderPath);

            try
            {
                var segments = MasterCopyRules.FolderSegments(folderPath);
                if (segments.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "folderPath is required, e.g. 'Motors' or 'Motors/Pumps'");
                }

                var root = RequireProjectLibrary();
                var parent = RequireTypeFolder(root, segments.Take(segments.Count - 1).ToList());
                var name = segments[segments.Count - 1];
                MasterCopyRules.CheckNotPresent(name, parent.Folders.Select(folder => folder.Name), $"The type folder '{string.Join("/", segments.Take(segments.Count - 1))}'");

                SaveLibraryBackup(backupDirectory, ProjectLibraryBackupFile, ReadTypeTree(root, ProjectLibraryName));
                parent.Folders.Create(name);

                return ReadTypeTree(root, ProjectLibraryName);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreateLibraryTypeFolder", PortalErrorCode.WriteFailed), "CreateLibraryTypeFolder", string.Empty, folderPath ?? string.Empty);
            }
        }

        private static JsonObject ReadTypeTree(LibraryTypeFolder root, string? library)
        {
            return new JsonObject
            {
                ["library"] = library,
                ["folders"] = new JsonArray(LibraryTypeReader.Folders(root).Select(entry => (JsonNode)entry.Path).ToArray()),
                ["types"] = LibraryTypeReader.Read(root)
            };
        }

        private static LibraryTypeFolder RequireTypeFolder(LibraryTypeFolder root, IReadOnlyList<string> segments)
        {
            if (segments.Count == 0)
            {
                return root;
            }

            var path = string.Join("/", segments) + "/";
            var found = LibraryTypeReader.Folders(root).FirstOrDefault(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));

            return found.Folder
                ?? throw new PortalException(PortalErrorCode.NotFound, $"The project library has no type folder '{path}'. It has: {MasterCopyRules.List(LibraryTypeReader.Folders(root).Select(entry => entry.Path))}");
        }
    }
}
