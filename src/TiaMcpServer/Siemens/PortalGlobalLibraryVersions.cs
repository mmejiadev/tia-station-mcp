using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// A global library past its first publish: new versions of the types it holds, and the library archived and retrieved.
    ///
    /// Measured 2026-11-24 on TIA Portal V21: <c>ProjectLibrary.UpdateLibrary</c> gives a global library holding a type the
    /// project's released versions it lacks, with their GUIDs, and the newest as its default; a version in work stays behind.
    /// <c>UserGlobalLibrary.Archive</c> refuses a library with unsaved changes and a target already holding the name, and writes
    /// one <c>.zal21</c> file; <c>GlobalLibraries.Retrieve</c> into an empty folder unpacks the library into a folder of its own
    /// there and opens it, and into a folder holding anything is cancelled ("The operation was canceled").
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Brings the new released versions of project library types to a global library that already holds them.</summary>
        /// <param name="globalLibraryPath">The global library file; it must not be open.</param>
        /// <param name="typeNames">The types, each already in the global library.</param>
        /// <param name="backupDirectory">Where the library is archived before it changes.</param>
        /// <returns><c>{ library, added: [ { type, versions } ], types }</c>; and <c>warning</c> when it could not be closed.</returns>
        /// <exception cref="PortalException">See <see cref="GlobalLibraryUpdateRules"/>; or the library is open, or a type is not in the project library.</exception>
        public JsonObject UpdateGlobalLibraryTypes(string globalLibraryPath, IReadOnlyList<string> typeNames, string backupDirectory)
        {
            _logger?.LogInformation("Bringing new versions of {Types} to {Library}...", string.Join(", ", typeNames ?? Array.Empty<string>()), globalLibraryPath);

            try
            {
                var types = LibraryTypeReader.ByName(RequireProjectLibrary());
                LibraryCopyRules.CheckRequested(typeNames!, types.Keys, LibraryCopyRules.ProjectLibrary);
                var selected = typeNames!.Select(name => types[name].Type).ToList();

                var (added, closeWarning) = GlobalLibraryAccess.UseForWriting(RequirePortal(), globalLibraryPath, backupDirectory, global => PushNewVersions(global, selected));

                return WithWarning(added, closeWarning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "UpdateGlobalLibraryTypes", PortalErrorCode.WriteFailed), "UpdateGlobalLibraryTypes", globalLibraryPath, string.Join(", ", typeNames ?? Array.Empty<string>()));
            }
        }

        /// <summary>Archives a global library into one compressed file, as TIA Portal's Archive does; the library is not changed.</summary>
        /// <param name="globalLibraryPath">The global library file.</param>
        /// <param name="targetDirectory">The folder the archive is written to; it is created when missing.</param>
        /// <returns><c>{ library, archive }</c>; and <c>warning</c> when the library could not be closed.</returns>
        /// <exception cref="PortalException">No library file there, an archive of its name already in the folder, or TIA Portal refused (unsaved changes in a library open there).</exception>
        public JsonObject ArchiveGlobalLibrary(string globalLibraryPath, string targetDirectory)
        {
            _logger?.LogInformation("Archiving global library {Library} to {Directory}...", globalLibraryPath, targetDirectory);

            try
            {
                var directory = FullDirectoryPath(targetDirectory ?? string.Empty);
                var file = new FileInfo((globalLibraryPath ?? string.Empty).Trim());
                var archive = Path.Combine(directory, GlobalLibraryAccess.ArchiveName(file));
                if (File.Exists(archive))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"'{archive}' already exists, and TIA Portal does not write over it; choose another folder. Nothing was written");
                }

                var (written, warning) = GlobalLibraryAccess.Use(RequirePortal(), globalLibraryPath!, global => global is UserGlobalLibrary user
                    ? GlobalLibraryAccess.Archive(user, file, directory)
                    : throw new PortalException(PortalErrorCode.InvalidState, $"'{global.Name}' is a {global.GetType().Name}; only a user's global library is archived"));

                return WithWarning(new JsonObject { ["library"] = file.FullName, ["archive"] = written }, warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "ArchiveGlobalLibrary", PortalErrorCode.ExportFailed), "ArchiveGlobalLibrary", globalLibraryPath, string.Empty);
            }
        }

        /// <summary>Retrieves an archived global library into an empty folder, and leaves it closed.</summary>
        /// <param name="archivePath">The archive, e.g. <c>C:\Backups\Faceplates.zal21</c>.</param>
        /// <param name="targetDirectory">An empty or new folder; the library makes a folder of its own there.</param>
        /// <returns><c>{ library, types }</c>: the retrieved library's file and its types; and <c>warning</c> when it could not be closed.</returns>
        /// <exception cref="PortalException">No archive there, the folder holds something, or TIA Portal refused the archive.</exception>
        public JsonObject RetrieveGlobalLibrary(string archivePath, string targetDirectory)
        {
            _logger?.LogInformation("Retrieving global library {Archive} into {Directory}...", archivePath, targetDirectory);

            try
            {
                var archive = new FileInfo((archivePath ?? string.Empty).Trim());
                if (string.IsNullOrWhiteSpace(archivePath) || !archive.Exists)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"There is no library archive at '{archivePath}'; give the .zalNN file");
                }

                var directory = RequireEmptyRetrieveDirectory(targetDirectory);
                var retrieved = RequirePortal().GlobalLibraries.Retrieve(archive, new DirectoryInfo(directory), OpenMode.ReadOnly);
                var library = retrieved.Path.FullName;
                var closeWarning = GlobalLibraryAccess.TryClose(retrieved);

                var (types, warning) = GlobalLibraryAccess.Use(RequirePortal(), library, global => LibraryTypeReader.Read(global.TypeFolder));

                return WithWarning(new JsonObject { ["library"] = library, ["types"] = types }, closeWarning ?? warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "RetrieveGlobalLibrary", PortalErrorCode.WriteFailed), "RetrieveGlobalLibrary", archivePath, string.Empty);
            }
        }

        /// <remarks>Measured 2026-11-24: TIA Portal cancels a retrieve into a folder that holds anything, with no word of why.</remarks>
        private static string RequireEmptyRetrieveDirectory(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "targetDirectory is required: an empty or new folder");
            }

            var directory = FullDirectoryPath(targetDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{directory}' is not empty, and TIA Portal cancels a retrieve into a folder that holds anything; give an empty or new one");
            }

            Directory.CreateDirectory(directory);

            return directory;
        }

        private JsonObject PushNewVersions(UserGlobalLibrary global, IReadOnlyList<LibraryType> selected)
        {
            var held = LibraryTypeReader.ByName(global.TypeFolder);
            var expected = selected.ToDictionary(type => type.Name, type => GlobalLibraryUpdateRules.RequireNewVersions(type.Name, Identities(type), held.TryGetValue(type.Name, out var there) ? Identities(there.Type) : null), StringComparer.OrdinalIgnoreCase);
            RequireDependenciesArrive(selected, expected, held);

            CurrentProject!.ProjectLibrary.UpdateLibrary(selected.Select(type => (ILibraryTypeOrFolderSelection)type).ToList(), global);

            var after = LibraryTypeReader.ByName(global.TypeFolder);
            foreach (var type in selected)
            {
                GlobalLibraryUpdateRules.RequireArrived(type.Name, expected[type.Name], after.TryGetValue(type.Name, out var arrived) ? Identities(arrived.Type) : Array.Empty<LibraryVersionIdentity>());
            }

            return new JsonObject
            {
                ["library"] = global.Path?.FullName,
                ["added"] = new JsonArray(selected.Select(type => (JsonNode)new JsonObject { ["type"] = type.Name, ["versions"] = new JsonArray(expected[type.Name].Select(number => (JsonNode)LibraryVersionNumber.Format(number)).ToArray()) }).ToArray()),
                ["types"] = LibraryTypeReader.Read(global.TypeFolder)
            };
        }

        private static void RequireDependenciesArrive(IReadOnlyList<LibraryType> selected, Dictionary<string, IReadOnlyList<Version>> expected, Dictionary<string, (string Folder, LibraryType Type)> held)
        {
            var newVersions = selected.SelectMany(type => expected[type.Name].Select(number => FindVersion(type, number))).ToList();
            var available = new HashSet<Guid>(held.Values.SelectMany(entry => entry.Type.Versions.Select(version => version.Guid)).Concat(newVersions.Select(version => version.Guid)));

            foreach (var type in selected)
            {
                var needed = expected[type.Name].SelectMany(number => FindVersion(type, number).Dependencies.Select(dependency => (dependency.TypeObject.Name, LibraryVersionNumber.Normalise(dependency.VersionNumber), dependency.Guid)));
                GlobalLibraryUpdateRules.RequireDependenciesPresent(type.Name, needed, available);
            }
        }

        private static List<LibraryVersionIdentity> Identities(LibraryType type)
        {
            return type.Versions.Select(version => new LibraryVersionIdentity(version.VersionNumber, version.Guid, LibraryTypeReader.IsReleased(version))).ToList();
        }
    }
}
