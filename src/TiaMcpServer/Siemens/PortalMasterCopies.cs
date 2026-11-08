using Microsoft.Extensions.Logging;
using Siemens.Engineering.Library.MasterCopies;
using Siemens.Engineering.SW;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Master copies of the project library: reading them (a global library's too), making one from a block, a tag table
    /// or a PLC data type, its folders, and creating an object in a program from one. Measured 2026-11-01 on TIA Portal
    /// V20: a block and a tag table become a master copy, a WinCC Unified screen cannot, and <c>CreateFrom</c> in its
    /// default mode renames without a word, so what is already there is refused first and <c>ThrowIfExists</c> asked for.
    /// </remarks>
    public partial class Portal
    {
        private const string MasterCopyBackupFile = "master-copies.json";

        /// <summary>Reads the master copies of the project library, or of a global library, with their folders.</summary>
        /// <param name="globalLibraryPath">A global library file, opened read-only; empty for the open project's library.</param>
        /// <returns><c>{ library, folders, masterCopies: [ { name, path, folder, author, created, contents } ] }</c>.</returns>
        /// <exception cref="PortalException">No project is open, or the global library file is not there.</exception>
        public JsonObject GetMasterCopies(string globalLibraryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(globalLibraryPath))
                {
                    return WithLibraryName(MasterCopyReader.Read(RequireMasterCopyRoot()), ProjectLibraryName);
                }

                var (read, warning) = GlobalLibraryAccess.Use(RequirePortal(), globalLibraryPath, global =>
                    WithLibraryName(MasterCopyReader.Read(global.MasterCopyFolder), global.Path?.FullName));

                return WithWarning(read, warning);
            }
            catch (Exception ex)
            {
                throw DecorateMasterCopyFailure(AsLibraryFailure(ex, "GetMasterCopies", PortalErrorCode.ExportFailed), "GetMasterCopies", globalLibraryPath, string.Empty);
            }
        }

        /// <summary>Creates a master copy folder in the project library.</summary>
        /// <param name="folderPath">The new folder's path, e.g. <c>Motors/Pumps</c>; the folders above it must exist.</param>
        /// <param name="backupDirectory">Where the library's master copies before the change are listed.</param>
        /// <returns>The project library's master copies and folders, read back.</returns>
        /// <exception cref="PortalException">The path is empty or not one, a folder above it is missing, or it is there already.</exception>
        public JsonObject CreateMasterCopyFolder(string folderPath, string backupDirectory)
        {
            _logger?.LogInformation("Creating master copy folder {Folder}...", folderPath);

            try
            {
                var segments = MasterCopyRules.FolderSegments(folderPath);
                if (segments.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "folderPath is required, e.g. 'Motors' or 'Motors/Pumps'");
                }

                var root = RequireMasterCopyRoot();
                var parent = RequireMasterCopyFolder(root, segments.Take(segments.Count - 1).ToList());
                var name = segments[segments.Count - 1];
                MasterCopyRules.CheckNotPresent(name, parent.Folder.Folders.Select(folder => folder.Name), MasterCopyRules.FolderPlace(parent.Names));

                SaveLibraryBackup(backupDirectory, MasterCopyBackupFile, MasterCopyReader.Read(root));
                parent.Folder.Folders.Create(name);

                return WithLibraryName(MasterCopyReader.Read(root), ProjectLibraryName);
            }
            catch (Exception ex)
            {
                throw DecorateMasterCopyFailure(AsLibraryFailure(ex, "CreateMasterCopyFolder", PortalErrorCode.WriteFailed), "CreateMasterCopyFolder", string.Empty, folderPath ?? string.Empty);
            }
        }

        /// <summary>Makes a master copy in the project library from a block, a tag table or a PLC data type of a program.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="source">The object, by kind and full path in the program.</param>
        /// <param name="folderPath">The master copy folder to put it in; empty for the top folder.</param>
        /// <param name="backupDirectory">Where the library's master copies before the change are listed.</param>
        /// <returns>The new master copy, as <see cref="GetMasterCopies"/> describes one.</returns>
        /// <exception cref="PortalException">The object or the folder is missing, a block or type path is a name pattern, or the folder already holds a master copy of that name.</exception>
        /// <remarks>Nothing in the program changes; the object is copied into the library as it stands.</remarks>
        public JsonObject CreateMasterCopy(string softwarePath, MasterCopySource source, string folderPath, string backupDirectory)
        {
            _logger?.LogInformation("Creating a master copy of {Source} from {Software}...", source?.Path, softwarePath);

            try
            {
                if (source == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "The source is required: its kind and its full path in the program");
                }

                var software = RequireSoftware(softwarePath);
                var root = RequireMasterCopyRoot();
                var folder = RequireMasterCopyFolder(root, MasterCopyRules.FolderSegments(folderPath));
                var item = RequireMasterCopySource(software, softwarePath, source);
                MasterCopyRules.CheckNotPresent(item.Name, folder.Folder.MasterCopies.Select(copy => copy.Name), MasterCopyRules.FolderPlace(folder.Names));

                SaveLibraryBackup(backupDirectory, MasterCopyBackupFile, MasterCopyReader.Read(root));
                var created = folder.Folder.MasterCopies.Create(item.Source);

                return MasterCopyReader.Describe(MasterCopyRules.Join(folder.Names, string.Empty), created);
            }
            catch (Exception ex)
            {
                throw DecorateMasterCopyFailure(AsLibraryFailure(ex, "CreateMasterCopy", PortalErrorCode.WriteFailed), "CreateMasterCopy", softwarePath, source?.Path ?? string.Empty);
            }
        }

        /// <summary>Creates a block, a tag table or a PLC data type in a program from a master copy of the project library.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="masterCopyPath">The master copy, e.g. <c>Motors/FB_Motor</c>, as <see cref="GetMasterCopies"/> gives its path.</param>
        /// <param name="groupPath">The group in the program to create it in; empty for the root.</param>
        /// <param name="backupDirectory">Where the program's blocks, types and tag tables are exported before anything is created.</param>
        /// <returns><c>{ name, kind, group, masterCopy }</c>.</returns>
        /// <exception cref="PortalException">
        /// The master copy, its folder or the group is missing; it holds anything but exactly one block, tag table or PLC
        /// data type; or the program already holds a block, data type or tag table of that name.
        /// </exception>
        /// <remarks>
        /// Blocks and PLC data types share their names in a program, so each is checked against both. A tag table's own
        /// tags are not named in its content description, and TIA Portal renames one the program already holds without a
        /// word (measured 2026-11-08), so <see cref="TagTableFromMasterCopy"/> checks the new table and takes it back.
        /// </remarks>
        public JsonObject CreateFromMasterCopy(string softwarePath, string masterCopyPath, string groupPath, string backupDirectory)
        {
            _logger?.LogInformation("Creating {MasterCopy} in {Software}...", masterCopyPath, softwarePath);

            try
            {
                var software = RequireOfflineSoftware(softwarePath);
                var (folderSegments, name) = MasterCopyRules.ParseMasterCopyPath(masterCopyPath);
                var folder = RequireMasterCopyFolder(RequireMasterCopyRoot(), folderSegments);
                var copy = MasterCopyReader.Find(folder.Folder, name)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"{MasterCopyRules.FolderPlace(folder.Names)} has no master copy '{name}'. It holds: {MasterCopyRules.List(folder.Folder.MasterCopies.Select(held => held.Name))}");

                var contents = MasterCopyReader.Contents(copy);
                var kind = MasterCopyRules.RequireSingleKind(copy.Name, contents.Select(content => (content.TypeName, content.Kind)).ToList());
                MasterCopyRules.CheckNotPresent(contents[0].Name, NamesInProgram(software, softwarePath, kind), $"The program '{softwarePath}'");

                var create = RequireCreator(software, softwarePath, kind, groupPath ?? string.Empty);
                SaveProgramBackup(software, backupDirectory);
                var created = create(copy);

                return new JsonObject
                {
                    ["name"] = created,
                    ["kind"] = MasterCopyRules.Name(kind),
                    ["group"] = groupPath ?? string.Empty,
                    ["masterCopy"] = MasterCopyRules.Join(folder.Names, copy.Name)
                };
            }
            catch (Exception ex)
            {
                throw DecorateMasterCopyFailure(AsLibraryFailure(ex, "CreateFromMasterCopy", PortalErrorCode.WriteFailed), "CreateFromMasterCopy", softwarePath, masterCopyPath ?? string.Empty);
            }
        }

        private (IMasterCopySource Source, string Name) RequireMasterCopySource(PlcSoftware software, string softwarePath, MasterCopySource source)
        {
            return source.Kind switch
            {
                MasterCopySourceKind.Block => AsMasterCopySource(FindBlock(softwarePath, RequireExactObjectName(source)), block => block.Name, source),
                MasterCopySourceKind.TagTable => AsMasterCopySource(RequireTable(software, source.Path), table => table.Name, source),
                MasterCopySourceKind.PlcType => AsMasterCopySource(FindType(softwarePath, RequireExactObjectName(source)), type => type.Name, source),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised master copy source kind: {source.Kind}")
            };
        }

        /// <remarks>
        /// Not every block is a master copy source in Openness V20: the concrete OB, FB, FC and data block classes are,
        /// the <c>PlcBlock</c> they share is not (measured 2026-11-06 by reflection), so the cast is checked rather than assumed.
        /// </remarks>
        private static (IMasterCopySource Source, string Name) AsMasterCopySource<T>(T? found, Func<T, string> nameOf, MasterCopySource source)
            where T : class
        {
            if (found == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"No {MasterCopyRules.Name(source.Kind)} at '{source.Path}' in the program; give its full path, e.g. 'Group/Name'");
            }

            return found is IMasterCopySource item
                ? (item, nameOf(found))
                : throw new PortalException(PortalErrorCode.InvalidState, $"'{source.Path}' is a {found.GetType().Name}, which TIA Portal cannot make a master copy of");
        }

        /// <remarks>Blocks and PLC data types may not share a name in one program, so either is checked against both.</remarks>
        private IEnumerable<string> NamesInProgram(PlcSoftware software, string softwarePath, MasterCopySourceKind kind)
        {
            return kind switch
            {
                MasterCopySourceKind.Block => BlockAndTypeNames(softwarePath),
                MasterCopySourceKind.TagTable => new TagTableLocator().AllNames(software.TagTableGroup),
                MasterCopySourceKind.PlcType => BlockAndTypeNames(softwarePath),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised master copy source kind: {kind}")
            };
        }

        private IEnumerable<string> BlockAndTypeNames(string softwarePath)
        {
            return FindBlocks(softwarePath).Select(block => block.Name).Concat(FindTypes(softwarePath).Select(type => type.Name));
        }

        /// <summary>The group is found before anything is written, so a missing one is refused, not reported as a failed backup.</summary>
        /// <remarks><c>ThrowIfExists</c> as well as the check before: the default mode renames the new object without a word (measured 2026-11-01).</remarks>
        private Func<MasterCopy, string> RequireCreator(PlcSoftware software, string softwarePath, MasterCopySourceKind kind, string groupPath)
        {
            switch (kind)
            {
                case MasterCopySourceKind.Block:
                    var blocks = GetPlcBlockGroupByPath(softwarePath, groupPath) ?? throw MissingGroup(softwarePath, kind, groupPath);
                    return copy => blocks.Blocks.CreateFrom(copy, MasterCopyMode.ThrowIfExists).Name;
                case MasterCopySourceKind.TagTable:
                    var tables = new TagTableLocator().FindGroup(software.TagTableGroup, groupPath) ?? throw MissingGroup(softwarePath, kind, groupPath);
                    return copy => TagTableFromMasterCopy.Create(software.TagTableGroup, tables, copy);
                case MasterCopySourceKind.PlcType:
                    var types = GetPlcTypeGroupByPath(softwarePath, groupPath) ?? throw MissingGroup(softwarePath, kind, groupPath);
                    return copy => types.Types.CreateFrom(copy, MasterCopyMode.ThrowIfExists).Name;
                default:
                    throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised master copy source kind: {kind}");
            }
        }

        /// <remarks>
        /// Blocks and types as ImportBlock keeps them, and every tag table besides: a tag table from a master copy can
        /// bring tags into the program's one tag namespace, so its tables are part of the state before.
        /// </remarks>
        private void SaveProgramBackup(PlcSoftware software, string backupDirectory)
        {
            ProgramBackup.Save(software, backupDirectory, _logger);
            new TagTableExporter(_logger).ExportAll(software.TagTableGroup, backupDirectory);
        }

        private MasterCopySystemFolder RequireMasterCopyRoot()
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project first: the project library is the open project's");
            }

            return CurrentProject!.ProjectLibrary.MasterCopyFolder;
        }

        private static (MasterCopyFolder Folder, IReadOnlyList<string> Names) RequireMasterCopyFolder(MasterCopyFolder root, IReadOnlyList<string> segments)
        {
            return MasterCopyReader.FindFolder(root, segments)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"The project library has no master copy folder '{MasterCopyRules.Join(segments, string.Empty)}'. It has: {MasterCopyRules.List(MasterCopyReader.Folders(root).Select(entry => entry.Path))}");
        }

        /// <remarks>Blocks and types are looked up by a name that may be a pattern (FindBlock, FindType); a master copy is made of one, named exactly.</remarks>
        private string RequireExactObjectName(MasterCopySource source)
        {
            var kind = MasterCopyRules.Name(source.Kind);
            if (string.IsNullOrWhiteSpace(source.Path))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"The {kind}'s path is required, e.g. 'Group/Name'");
            }

            if (ProjectPath.Parse(source.Path).Name.IndexOfAny(_regexChars) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{source.Path}' is a name pattern; a master copy is made from exactly one {kind}, named exactly");
            }

            return source.Path;
        }

        private static PortalException MissingGroup(string softwarePath, MasterCopySourceKind kind, string groupPath)
        {
            return new PortalException(PortalErrorCode.NotFound, $"The program '{softwarePath}' has no {MasterCopyRules.Name(kind)} group '{groupPath}'; create it first, or leave groupPath empty for the root");
        }

        private static JsonObject WithLibraryName(JsonObject read, string? library)
        {
            read["library"] = library;

            return read;
        }

        private PortalException DecorateMasterCopyFailure(PortalException pex, string operation, string place, string path)
        {
            pex.Data["place"] = place;
            pex.Data["path"] = path;

            _logger?.LogError(pex, "{Operation} failed for {Place} {Path}", operation, place, path);
            return pex;
        }
    }
}
