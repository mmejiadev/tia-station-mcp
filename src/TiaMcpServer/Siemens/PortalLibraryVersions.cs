using Microsoft.Extensions.Logging;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.Types;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Library types past the first copy: a global library of one's own, publishing to it, a new version in work,
    /// and a PLC data type brought into a program. Measured 2026-11-01 on TIA Portal V20.
    /// </remarks>
    public partial class Portal
    {
        private const string PlainTypeKind = nameof(LibraryType);

        /// <summary>Creates a new, empty global library and leaves it closed.</summary>
        /// <param name="directory">The folder to create it in; the library makes a folder of its own there.</param>
        /// <param name="libraryName">The library's name.</param>
        /// <returns><c>{ library }</c>: the path of its <c>.al20</c> file, for the tools that take one; and <c>warning</c> when it could not be closed.</returns>
        /// <exception cref="PortalException">The name is not one a folder can have, a library of that name is already there, or TIA Portal refused the name or the folder.</exception>
        public JsonObject CreateGlobalLibrary(string directory, string libraryName)
        {
            _logger?.LogInformation("Creating global library {Library} in {Directory}...", libraryName, directory);

            try
            {
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(libraryName))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "directory and libraryName are both required");
                }

                if (!IsFullPath(directory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"directory must be a full path, as TIA Portal refuses a relative one; got '{directory}'");
                }

                if (libraryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"libraryName becomes a folder, and '{libraryName}' cannot name one; the folder it goes in is directory");
                }

                if (Directory.Exists(Path.Combine(directory, libraryName)))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"'{Path.Combine(directory, libraryName)}' already exists; nothing was created");
                }

                // Not the DirectoryInfo Directory.CreateDirectory returns: it prints only the folder's name, and Openness
                // refuses that as "cannot be a relative path" (measured 2026-11-01).
                Directory.CreateDirectory(directory);
                var created = RequirePortal().GlobalLibraries.Create<UserGlobalLibrary>(new DirectoryInfo(directory), libraryName);
                var path = created.Path.FullName;

                return WithWarning(new JsonObject { ["library"] = path }, GlobalLibraryAccess.TryClose(created));
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreateGlobalLibrary", PortalErrorCode.WriteFailed), "CreateGlobalLibrary", directory, libraryName);
            }
        }

        /// <summary>Publishes project library types to a global library, with the types they depend on.</summary>
        /// <param name="globalLibraryPath">The global library file; it must not be open.</param>
        /// <param name="typeNames">The types, each with a released version; neither they nor any type they depend on may be in the global library yet.</param>
        /// <param name="backupDirectory">Where the library is archived before it changes.</param>
        /// <returns><c>{ library, types }</c>: every type the library holds afterwards; and <c>warning</c> when it could not be closed.</returns>
        /// <exception cref="PortalException">See <see cref="LibraryPublishRules"/> and <see cref="LibraryCopyRules"/>; or the library is open.</exception>
        public JsonObject PublishLibraryTypes(string globalLibraryPath, IReadOnlyList<string> typeNames, string backupDirectory)
        {
            _logger?.LogInformation("Publishing library types {Types} to {Library}...", string.Join(", ", typeNames ?? Array.Empty<string>()), globalLibraryPath);

            try
            {
                var types = LibraryTypeReader.ByName(RequireProjectLibrary());
                LibraryPublishRules.Check(typeNames!, types.Keys, name => LibraryTypeReader.Versions(types[name].Type));
                var selected = typeNames!.Select(name => types[name].Type).ToList();

                var (_, closeWarning) = GlobalLibraryAccess.UseForWriting(RequirePortal(), globalLibraryPath, backupDirectory, global => PublishTo(global, selected));

                return ReadPublished(globalLibraryPath, closeWarning);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "PublishLibraryTypes", PortalErrorCode.WriteFailed), "PublishLibraryTypes", globalLibraryPath, string.Join(", ", typeNames ?? Array.Empty<string>()));
            }
        }

        /// <summary>Starts a new version in work of a project library type, from its newest released version.</summary>
        /// <param name="typeName">A type Openness has no class for, such as a WinCC Unified faceplate.</param>
        /// <param name="backupDirectory">Where the type's versions before are written.</param>
        /// <returns>The type, read back; the new version is numbered by TIA Portal.</returns>
        /// <exception cref="PortalException">The type does not exist, has a version in work already, has none released, or is a PLC type.</exception>
        /// <remarks>
        /// Measured 2026-10-30 and 2026-11-01: a faceplate's new version takes one call and two at once are refused
        /// ("Parallel editing is not supported"); a PLC data type refuses it, needing a test instance, which is edited
        /// in TIA Portal. The new version holds what the released one held: its content is still changed by hand.
        /// </remarks>
        public JsonObject EditLibraryType(string typeName, string backupDirectory)
        {
            _logger?.LogInformation("Starting a new version of library type {Type}...", typeName);

            try
            {
                var found = RequireLibraryType(typeName);

                if (found.Type.GetType().Name != PlainTypeKind)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"'{found.Type.Name}' is a {found.Type.GetType().Name}; a new version of it needs a test instance, so edit it in TIA Portal");
                }

                var versions = LibraryTypeReader.Versions(found.Type);
                if (versions.Any(version => !version.IsReleased))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"'{found.Type.Name}' already has a version in work, and TIA Portal edits one at a time; ReleaseLibraryType releases it");
                }

                var newest = LibraryPublishRules.ReleasedVersion(found.Type.Name, versions, null);
                SaveLibraryBackup(backupDirectory, $"{SnapshotFileName.For(found.Type.Name)}.json", LibraryTypeReader.Describe(found.Folder, found.Type));
                FindVersion(found.Type, newest).Edit();

                return LibraryTypeReader.Describe(found.Folder, found.Type);
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "EditLibraryType", PortalErrorCode.WriteFailed), "EditLibraryType", string.Empty, typeName ?? string.Empty);
            }
        }

        /// <summary>Brings a project library PLC data type into a program, as an instance linked to the type.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="typeName">The PLC data type.</param>
        /// <param name="version">The released version, or null for the newest.</param>
        /// <returns><c>{ name, type, version }</c>.</returns>
        /// <exception cref="PortalException">The type is not a PLC data type, has no such released version, or the program already holds a type of that name or one it depends on.</exception>
        /// <remarks>
        /// Measured 2026-11-01: <c>CreateFrom</c> on a program already holding the type answers with it and no error, so
        /// that is refused here, in the program's subgroups too, and for what the type depends on alike, as what TIA Portal
        /// does with a dependency already there was not measured. Nothing existing changes, so there is no earlier state to
        /// keep. A block type is not taken yet: bringing one in was not measured.
        /// </remarks>
        public JsonObject CreatePlcTypeFromLibrary(string softwarePath, string typeName, Version? version)
        {
            _logger?.LogInformation("Creating library type {Type} in {Software}...", typeName, softwarePath);

            try
            {
                var software = RequireSoftware(softwarePath);
                var found = RequireLibraryType(typeName);

                if (!(found.Type is PlcTypeLibraryType))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"'{found.Type.Name}' is a {found.Type.GetType().Name}, not a PLC data type");
                }

                var held = LibraryTypeReader.WithReleasedDependencies(found.Type).Where(name => HoldsPlcType(software.TypeGroup, name)).ToList();
                if (held.Count > 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"'{softwarePath}' already holds a PLC data type '{string.Join("', '", held)}', which this would bring; nothing was created");
                }

                var number = LibraryPublishRules.ReleasedVersion(found.Type.Name, LibraryTypeReader.Versions(found.Type), version);
                var created = software.TypeGroup.Types.CreateFrom((PlcTypeLibraryTypeVersion)FindVersion(found.Type, number));

                return new JsonObject { ["name"] = created.Name, ["type"] = found.Type.Name, ["version"] = LibraryVersionNumber.Format(number) };
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreatePlcTypeFromLibrary", PortalErrorCode.WriteFailed), "CreatePlcTypeFromLibrary", softwarePath, typeName ?? string.Empty);
            }
        }

        /// <remarks>
        /// What the global library already holds is refused before anything is added, and what was brought is read back
        /// after: a failure in either closes the library unsaved, so the file keeps what it had. Only names are read
        /// here, so a type this publish never touches cannot stop it.
        /// </remarks>
        private bool PublishTo(UserGlobalLibrary global, IReadOnlyList<LibraryType> selected)
        {
            var brought = selected.SelectMany(LibraryTypeReader.WithReleasedDependencies).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            LibraryCopyRules.CheckNotPresent(brought, LibraryTypeReader.ByName(global.TypeFolder).Keys, LibraryCopyRules.GlobalLibrary);

            CurrentProject!.ProjectLibrary.UpdateLibrary(selected.Select(type => (ILibraryTypeOrFolderSelection)type).ToList(), global);
            LibraryPublishRules.CheckPublished(brought, LibraryTypeReader.ByName(global.TypeFolder).Keys);

            return true;
        }

        /// <remarks>
        /// After the save: the publish is done, so a type in a state this server does not know is reported beside it,
        /// not as its failure.
        /// </remarks>
        private JsonObject ReadPublished(string globalLibraryPath, string? closeWarning)
        {
            try
            {
                var (read, warning) = GlobalLibraryAccess.Use(RequirePortal(), globalLibraryPath, global =>
                    new JsonObject { ["library"] = global.Path?.FullName, ["types"] = LibraryTypeReader.Read(global.TypeFolder) });

                return WithWarning(read, closeWarning ?? warning);
            }
            catch (PortalException unread) when (unread.Code == PortalErrorCode.InvalidState)
            {
                return WithWarning(new JsonObject { ["library"] = globalLibraryPath }, $"Published and saved, but reading the library back failed: {unread.Message.Trim()} {closeWarning}".Trim());
            }
        }

        /// <remarks><see cref="Path.IsPathRooted"/> also takes <c>C:Libs</c> and <c>\Libs</c>, which depend on the current directory.</remarks>
        private static bool IsFullPath(string directory)
        {
            return string.Equals(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        private static bool HoldsPlcType(PlcTypeGroup group, string typeName)
        {
            return group.Types.Any(type => string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                || group.Groups.Any(nested => HoldsPlcType(nested, typeName));
        }

        private (string Folder, LibraryType Type) RequireLibraryType(string typeName)
        {
            var types = LibraryTypeReader.ByName(RequireProjectLibrary());

            return types.TryGetValue(typeName ?? string.Empty, out var found)
                ? found
                : throw new PortalException(PortalErrorCode.NotFound, $"The project library has no type '{typeName}'. It holds: {string.Join(", ", types.Keys)}");
        }

        private static LibraryTypeVersion FindVersion(LibraryType type, Version number)
        {
            return type.Versions.First(version => LibraryVersionNumber.Normalise(version.VersionNumber) == number);
        }
    }
}
