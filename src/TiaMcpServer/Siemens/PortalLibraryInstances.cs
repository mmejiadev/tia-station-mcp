using Microsoft.Extensions.Logging;
using Siemens.Engineering.Library.Types;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// A program's instances of block and PLC data type library types: a block created from a type, instances moved to another
    /// version, and a new version started through an instance.
    ///
    /// Measured 2026-11-24 on TIA Portal V21. <c>PlcBlockComposition.CreateFrom(CodeBlockLibraryTypeVersion)</c> creates the
    /// block, inconsistent until compiled; on a program already holding the type's instance it answers with that one and no
    /// error when the version is the same, and creates <c>FC_Motor_1</c> without a word when it is another. And
    /// <c>LibraryType.UpdateProject</c>, <c>ProjectLibrary.UpdateProject</c> and <c>GlobalLibrary.UpdateProject</c> - every
    /// overload, compiled or not, in the root or a group, out of date or up to date, in a first or a second PLC - <b>removed</b>
    /// the instance from the program instead of updating it, without an error. So nothing here calls <c>UpdateProject</c>:
    /// an instance is moved by deleting it and creating it again from the version asked for, in the same group.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Creates a block in a program from a project library block type, as an instance linked to the type.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="typeName">The block type.</param>
        /// <param name="version">The released version, or null for the newest.</param>
        /// <param name="groupPath">The block group to create it in; empty for the root.</param>
        /// <returns><c>{ name, type, version, group }</c>.</returns>
        /// <exception cref="PortalException">The type is not a block type, has no such released version, the group is missing, or the program already holds a block or data type of its name or of one it depends on.</exception>
        public JsonObject CreatePlcBlockFromLibrary(string softwarePath, string typeName, Version? version, string groupPath)
        {
            _logger?.LogInformation("Creating library block type {Type} in {Software}...", typeName, softwarePath);

            try
            {
                RequireOfflineSoftware(softwarePath);
                var found = RequireLibraryType(typeName);
                var type = found.Type as CodeBlockLibraryType
                    ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{found.Type.Name}' is a {found.Type.GetType().Name}, not a block type; CreatePlcTypeFromLibrary takes a PLC data type");
                var group = GetPlcBlockGroupByPath(softwarePath, groupPath ?? string.Empty)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"The program '{softwarePath}' has no block group '{groupPath}'; create it first, or leave groupPath empty for the root");

                RequireNoneHeld(softwarePath, LibraryTypeReader.WithReleasedDependencies(type));
                var number = LibraryPublishRules.ReleasedVersion(type.Name, LibraryTypeReader.Versions(type), version);
                var created = RequireNamedAsType(group.Blocks.CreateFrom((CodeBlockLibraryTypeVersion)FindVersion(type, number)), type.Name);

                return new JsonObject { ["name"] = created.Name, ["type"] = type.Name, ["version"] = LibraryVersionNumber.Format(number), ["group"] = groupPath ?? string.Empty };
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreatePlcBlockFromLibrary", PortalErrorCode.WriteFailed), "CreatePlcBlockFromLibrary", softwarePath, typeName ?? string.Empty);
            }
        }

        /// <summary>Moves a program's instances of a block or PLC data type library type to another released version.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="typeName">The type.</param>
        /// <param name="version">The released version, or null for the newest.</param>
        /// <param name="backupDirectory">Where the program's blocks, types and tag tables are exported first.</param>
        /// <returns><c>{ type, version, moved: [ { name, from } ], compile: { errorsBefore, errorsAfter } }</c>.</returns>
        /// <exception cref="PortalException">
        /// The type is not a block or data type type, has no such released version, an instance is a function block, or the
        /// program compiles with more errors afterwards, in which case every instance is put back on the version it showed.
        /// </exception>
        public JsonObject UpdatePlcLibraryInstances(string softwarePath, string typeName, Version? version, string backupDirectory)
        {
            _logger?.LogInformation("Moving the instances of library type {Type} in {Software}...", typeName, softwarePath);

            try
            {
                var software = RequireOfflineSoftware(softwarePath);
                var type = RequirePlcLibraryType(typeName);
                var versions = LibraryTypeReader.Versions(type);
                var number = LibraryPublishRules.ReleasedVersion(type.Name, versions, version);
                var moving = FindPlcInstances(type, software).Where(instance => instance.Shown != number).ToList();

                var named = NameInstances(moving);
                if (moving.Count == 0)
                {
                    return DescribeMoved(type.Name, number, named, (0, 0));
                }

                RequireMovable((type.Name, softwarePath), versions, moving);
                var errorsBefore = CompileSoftware(softwarePath).ErrorCount;
                SaveProgramBackup(software, backupDirectory);

                return DescribeMoved(type.Name, number, named, (errorsBefore, MoveCheckingCompile(softwarePath, (type, number), moving, errorsBefore)));
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "UpdatePlcLibraryInstances", PortalErrorCode.WriteFailed), "UpdatePlcLibraryInstances", softwarePath, typeName ?? string.Empty);
            }
        }

        /// <summary>Starts the next version in work of a block or PLC data type type, through its instance in a program.</summary>
        /// <param name="type">The type.</param>
        /// <param name="softwarePath">The program holding an instance of its newest released version.</param>
        /// <param name="newest">The newest released version.</param>
        /// <remarks>See <see cref="LibraryInstanceRules"/>: <c>Edit()</c> is refused for such a type, <c>Edit(instance)</c> is not.</remarks>
        private void EditThroughInstance(LibraryType type, string softwarePath, Version newest)
        {
            if (string.IsNullOrWhiteSpace(softwarePath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{type.Name}' is a {type.GetType().Name}, which TIA Portal edits only through a test instance in a program: give softwarePath, the PLC software holding it");
            }

            var instances = FindPlcInstances(type, RequireOfflineSoftware(softwarePath));
            LibraryInstanceRules.RequireInstanceOn(type.Name, softwarePath, newest, instances.Select(instance => instance.Shown).ToList());

            FindVersion(type, newest).Edit(instances.First(instance => instance.Shown == newest).Info);
        }

        private LibraryType RequirePlcLibraryType(string typeName)
        {
            var found = RequireLibraryType(typeName);

            return IsPlcLibraryType(found.Type)
                ? found.Type
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{found.Type.Name}' is a {found.Type.GetType().Name}; this moves instances of block and PLC data type types (UpdateUnifiedFaceplates moves faceplates)");
        }

        private static bool IsPlcLibraryType(LibraryType type)
        {
            return type is CodeBlockLibraryType || type is PlcTypeLibraryType;
        }

        /// <summary>Every instance of the type in the program, with the version it shows.</summary>
        private static List<PlcInstance> FindPlcInstances(LibraryType type, PlcSoftware software)
        {
            return type.Versions
                .SelectMany(version => version.FindInstances(software).Select(info => new PlcInstance(LibraryVersionNumber.Normalise(version.VersionNumber), info)))
                .ToList();
        }

        /// <remarks>
        /// A function block's instance data blocks name it, and what deleting and creating it again does to them was not measured,
        /// so it is refused rather than tried on a program somebody depends on. An instance on a version in work is that
        /// version's test instance, see <see cref="LibraryInstanceRules.RequireNoneInWork"/>.
        /// </remarks>
        private static void RequireMovable((string TypeName, string SoftwarePath) place, IReadOnlyList<LibraryVersionInfo> versions, IReadOnlyList<PlcInstance> instances)
        {
            if (instances.Any(instance => instance.Info.LibraryTypeInstance is FB))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{place.TypeName}' is a function block, and moving its instance would delete and create it again under its instance data blocks, which was not measured; move it in TIA Portal. Nothing was changed");
            }

            var released = versions.Where(version => version.IsReleased).Select(version => version.Number).ToList();
            LibraryInstanceRules.RequireNoneInWork(place.TypeName, place.SoftwarePath, released, instances.Select(instance => instance.Shown).ToList());
        }

        private void RequireNoneHeld(string softwarePath, IEnumerable<string> names)
        {
            var held = names.Intersect(BlockAndTypeNames(softwarePath), StringComparer.OrdinalIgnoreCase).ToList();
            if (held.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{softwarePath}' already holds a block or data type '{string.Join("', '", held)}', which this would bring; TIA Portal would answer with the one there, or create another under a new name, without a word. Nothing was created");
            }
        }

        /// <remarks>Checked although the name was found free: a rename would be silent, and a block nobody asked for must not stay.</remarks>
        private static PlcBlock RequireNamedAsType(PlcBlock created, string typeName)
        {
            if (string.Equals(created.Name, typeName, StringComparison.OrdinalIgnoreCase))
            {
                return created;
            }

            var renamed = created.Name;
            created.Delete();
            throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal created '{renamed}' instead of '{typeName}', so the program held one already; it was deleted again");
        }

        /// <summary>One instance of a library type in a program, and the version it shows.</summary>
        private sealed class PlcInstance
        {
            public PlcInstance(Version shown, LibraryTypeInstanceInfo info)
            {
                Shown = shown;
                Info = info;
            }

            public Version Shown { get; }

            public LibraryTypeInstanceInfo Info { get; }
        }
    }
}
