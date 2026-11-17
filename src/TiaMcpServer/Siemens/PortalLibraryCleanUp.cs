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
    /// The project library as a whole: where its types are used, and cleaning out what nothing uses.
    ///
    /// Measured 2026-11-15 on TIA Portal V21, on a copy of a class project: <c>CleanUpLibrary(typeFolder,
    /// PreserveDefaultVersionOfUnusedTypes)</c> deleted every released version no instance and no other type used, keeping
    /// each type's default and its version in work - five versions of a faceplate went, the sixth stayed;
    /// <c>DeleteUnusedTypes</c> also deletes the types nothing uses at all. Neither can be undone, so the saved project is
    /// archived first, and what went is read back by comparing the library before and after.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Lists where the project uses its library types, with the status TIA Portal gives each instance.</summary>
        /// <returns><c>{ usages: [ { type, libraryVersion, device, instance, instanceVersion, status } ] }</c>.</returns>
        /// <exception cref="PortalException">No project is open, or TIA Portal failed.</exception>
        public JsonObject GetLibraryTypeUsage()
        {
            _logger?.LogInformation("Checking where the project library's types are used...");

            try
            {
                RequireProjectLibrary();

                var result = CurrentProject!.ProjectLibrary.UpdateCheck(CurrentProject, UpdateCheckMode.ReportOutOfDateAndUpToDate);

                return new JsonObject { ["usages"] = LibraryUsageReader.Read(result) };
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "GetLibraryTypeUsage", PortalErrorCode.ExportFailed), "GetLibraryTypeUsage", string.Empty, string.Empty);
            }
        }

        /// <summary>Deletes the project library's versions, and optionally types, that nothing uses.</summary>
        /// <param name="deleteUnusedTypes">True to delete types nothing uses as well; false keeps each type's default version.</param>
        /// <param name="backupDirectory">Where the project is archived first.</param>
        /// <returns><c>{ removed: [ "type version" ], types }</c>: what went, and the library afterwards.</returns>
        /// <exception cref="PortalException">No project is open, it has unsaved changes, or TIA Portal refused.</exception>
        public JsonObject CleanUpProjectLibrary(bool deleteUnusedTypes, string backupDirectory)
        {
            _logger?.LogInformation("Cleaning up the project library, unused types {DeleteUnusedTypes}...", deleteUnusedTypes);

            return RunLibraryMaintenance("CleanUpProjectLibrary", string.Empty, () =>
            {
                var folder = RequireProjectLibrary();
                var before = VersionsHeld(folder);
                var mode = deleteUnusedTypes ? CleanUpMode.DeleteUnusedTypes : CleanUpMode.PreserveDefaultVersionOfUnusedTypes;

                ArchiveProjectToBackup(backupDirectory, "project library");
                CurrentProject!.ProjectLibrary.CleanUpLibrary(new ILibraryTypeOrFolderSelection[] { folder }, mode);

                var after = VersionsHeld(folder);

                return new JsonObject
                {
                    ["removed"] = new JsonArray(before.Except(after).Select(gone => (JsonNode)gone).ToArray()),
                    ["types"] = LibraryTypeReader.Read(folder)
                };
            });
        }

        private static HashSet<string> VersionsHeld(LibraryTypeSystemFolder folder)
        {
            return new HashSet<string>(
                LibraryTypeReader.ByName(folder).Values.SelectMany(found =>
                    LibraryTypeReader.Versions(found.Type).Select(version => $"{found.Type.Name} {LibraryVersionNumber.Format(version.Number)}")),
                StringComparer.Ordinal);
        }
    }
}
