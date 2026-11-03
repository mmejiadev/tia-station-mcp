using Microsoft.Extensions.Logging;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Library types: reading the project library or a global one, copying types from a global library
    /// into the project, and releasing a version in work. Creating a WinCC Unified faceplate type, or
    /// drawing one, is not possible through Openness V20 (measured 2026-10-30): it is done once by hand
    /// in TIA Portal, and everything after it here.
    /// </remarks>
    public partial class Portal
    {
        private const string ProjectLibraryName = "project";

        private const string ProjectLibraryBackupFile = "project-library.json";

        /// <summary>Reads the types of the project library, or of a global library.</summary>
        /// <param name="globalLibraryPath">A global library file, opened read-only; empty for the open project's library.</param>
        /// <returns><c>{ library, types: [ { name, folder, kind, versions: [...] } ] }</c>.</returns>
        /// <exception cref="PortalException">No project is open, or the global library file is not there.</exception>
        public JsonObject GetLibraryTypes(string globalLibraryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(globalLibraryPath))
                {
                    return new JsonObject { ["library"] = ProjectLibraryName, ["types"] = LibraryTypeReader.Read(RequireProjectLibrary()) };
                }

                var (read, warning) = GlobalLibraryAccess.Use(RequirePortal(), globalLibraryPath, global =>
                    new JsonObject { ["library"] = global.Path?.FullName, ["types"] = LibraryTypeReader.Read(global.TypeFolder) });

                return WithWarning(read, warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "GetLibraryTypes", PortalErrorCode.ExportFailed), "GetLibraryTypes", globalLibraryPath, string.Empty);
            }
        }

        /// <summary>Copies types from a global library into the project library, with the types they depend on.</summary>
        /// <param name="globalLibraryPath">The global library file, opened read-only.</param>
        /// <param name="typeNames">The types to copy; neither they nor any type they depend on may be in the project library yet.</param>
        /// <param name="backupDirectory">Where the project library's types before the copy are written.</param>
        /// <returns>Every type the project library gained, read back.</returns>
        /// <exception cref="PortalException">See <see cref="LibraryCopyRules"/>; or TIA Portal refused the copy, in which case what it added is removed again.</exception>
        public JsonObject CopyLibraryTypes(string globalLibraryPath, IReadOnlyList<string> typeNames, string backupDirectory)
        {
            _logger?.LogInformation("Copying library types {Types} from {Library}...", string.Join(", ", typeNames ?? Array.Empty<string>()), globalLibraryPath);

            try
            {
                var project = RequireProjectLibrary();

                var (copied, warning) = GlobalLibraryAccess.Use(RequirePortal(), globalLibraryPath, global => CopyFrom(global, typeNames!, (project, backupDirectory)));

                return WithWarning(copied, warning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CopyLibraryTypes", PortalErrorCode.WriteFailed), "CopyLibraryTypes", globalLibraryPath, string.Join(", ", typeNames ?? Array.Empty<string>()));
            }
        }

        /// <summary>Releases a project library type's version in work.</summary>
        /// <param name="typeName">The type.</param>
        /// <param name="version">The number to release under, or null to keep the one the version in work has.</param>
        /// <param name="comment">The release comment, or empty.</param>
        /// <param name="backupDirectory">Where the type's versions before the release are written.</param>
        /// <returns>The type, read back.</returns>
        /// <exception cref="PortalException">See <see cref="LibraryReleaseRules"/>; or a type it depends on is still in work.</exception>
        /// <remarks>A release cannot be undone through Openness, so everything that can refuse it is checked first.</remarks>
        public JsonObject ReleaseLibraryType(string typeName, Version? version, string comment, string backupDirectory)
        {
            _logger?.LogInformation("Releasing library type {Type}...", typeName);

            try
            {
                var types = LibraryTypeReader.ByName(RequireProjectLibrary());
                if (!types.TryGetValue(typeName ?? string.Empty, out var found))
                {
                    throw new PortalException(PortalErrorCode.NotFound, $"The project library has no type '{typeName}'. It holds: {string.Join(", ", types.Keys)}");
                }

                var number = LibraryReleaseRules.Choose(found.Type.Name, LibraryTypeReader.Versions(found.Type), version);
                var inWork = found.Type.Versions.Single(candidate => !LibraryTypeReader.IsReleased(candidate));
                LibraryReleaseRules.RequireDependenciesReleased(found.Type.Name, inWork.Dependencies.Select(dependency => (dependency.TypeObject.Name, LibraryTypeReader.IsReleased(dependency))));

                SaveLibraryBackup(backupDirectory, $"{SnapshotFileName.For(found.Type.Name)}.json", LibraryTypeReader.Describe(found.Folder, found.Type));
                inWork.Release(CreateOrReleaseDependenciesMode.DoNotAutomaticallyCreateOrReleaseDependencies, number, Environment.UserName, comment ?? string.Empty);

                return LibraryTypeReader.Describe(found.Folder, found.Type);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "ReleaseLibraryType", PortalErrorCode.WriteFailed), "ReleaseLibraryType", string.Empty, typeName ?? string.Empty);
            }
        }

        /// <remarks>
        /// The selection is the global library's own types, found case aside in one walk. What the copy would
        /// bring, dependencies included, is checked against the project before anything is written.
        /// </remarks>
        private JsonObject CopyFrom(GlobalLibrary global, IReadOnlyList<string> typeNames, (LibraryTypeSystemFolder Project, string BackupDirectory) target)
        {
            var inGlobal = LibraryTypeReader.ByName(global.TypeFolder);
            LibraryCopyRules.CheckRequested(typeNames, inGlobal.Keys, LibraryCopyRules.GlobalLibrary);

            var selected = typeNames.Select(name => inGlobal[name].Type).ToList();
            var brought = selected.SelectMany(LibraryTypeReader.WithDependencies).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // One walk of the project library for the names and the backup alike.
            var held = LibraryTypeReader.All(target.Project).ToList();
            var before = (Types: (IReadOnlyCollection<string>)held.Select(entry => entry.Type.Name).ToList(), Folders: (IReadOnlyCollection<string>)LibraryTypeReader.Folders(target.Project).Select(entry => entry.Path).ToList());
            LibraryCopyRules.CheckNotPresent(brought, before.Types, LibraryCopyRules.ProjectLibrary);

            SaveLibraryBackup(target.BackupDirectory, ProjectLibraryBackupFile, new JsonArray(held.Select(entry => (JsonNode)LibraryTypeReader.Describe(entry.Folder, entry.Type)).ToArray()));

            try
            {
                global.UpdateLibrary(selected.Select(type => (ILibraryTypeOrFolderSelection)type).ToList(), CurrentProject!.ProjectLibrary);
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                // Everything the request could get wrong was refused above, so what fails here is TIA Portal or the environment.
                throw LibraryCopyUndo.Undo(target.Project, before, new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal failed the copy: {failure.Message.Trim()}", null, failure));
            }

            var gained = LibraryTypeReader.All(target.Project).Where(entry => !before.Types.Contains(entry.Type.Name, StringComparer.OrdinalIgnoreCase));

            return new JsonObject { ["copied"] = new JsonArray(gained.Select(entry => (JsonNode)LibraryTypeReader.Describe(entry.Folder, entry.Type)).ToArray()) };
        }

        private static JsonObject WithWarning(JsonObject content, string? warning)
        {
            if (warning != null)
            {
                content["warning"] = warning;
            }

            return content;
        }

        /// <remarks>Every write keeps the state before it; a library change cannot be undone through Openness, so this is the record of it.</remarks>
        private static void SaveLibraryBackup(string backupDirectory, string fileName, JsonNode before)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the library is read into it before it changes");
            }

            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, fileName), before.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private LibraryTypeSystemFolder RequireProjectLibrary()
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project first: the project library is the open project's");
            }

            return CurrentProject!.ProjectLibrary.TypeFolder;
        }

        private global::Siemens.Engineering.TiaPortal RequirePortal()
        {
            return _portal ?? throw new PortalException(PortalErrorCode.InvalidState, "Connect to TIA Portal first");
        }

        private PortalException AsLibraryFailure(Exception ex, string operation, PortalErrorCode code)
        {
            return ForgetEndedSession(ex, operation)
                ?? ex as PortalException
                ?? new PortalException(code, $"{operation} failed: {ex.Message}", null, ex);
        }

        private PortalException DecorateLibraryFailure(PortalException pex, string operation, string globalLibraryPath, string types)
        {
            pex.Data["globalLibraryPath"] = globalLibraryPath;
            pex.Data["types"] = types;

            _logger?.LogError(pex, "{Operation} failed for {GlobalLibraryPath} {Types}", operation, globalLibraryPath, types);
            return pex;
        }
    }
}
