using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Global libraries of one's own, new versions in work, instances moved between versions, and PLC data types from a library.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateGlobalLibrary"), Description("Create a new, empty global library in a folder and leave it closed; the library makes a folder of its own there. A library of that name already there is refused. A write policy names a library by its full folder, e.g. 'global-library/C:/Libraries/Faceplates', so a rule for one does not reach another of the same name. Answers with the path of its .alNN file, which GetLibraryTypes, CopyLibraryTypes and PublishLibraryTypes take.")]
        public static ResponseLibraryTypes CreateGlobalLibrary(
            [Description("directory: the folder to create it in, e.g. 'C:\\Libraries'")] string directory,
            [Description("libraryName: the library's name, e.g. 'Faceplates_Clase'")] string libraryName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var request = new Governance.ChangeRequest("CreateGlobalLibrary", ChangeTarget.NewGlobalLibrary(directory, libraryName), $"in {directory}");

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreateGlobalLibrary(directory, libraryName), $"Global library '{libraryName}' created."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating global library '{libraryName}'");
            }
        }

        [McpServerTool(Name = "PublishLibraryTypes"), Description("Publish project library types to a global library file, with the types they depend on, e.g. a faceplate drawn by hand, so other projects can take it with CopyLibraryTypes. Each type needs a released version, as TIA Portal V20 publishes a type only in work as nothing at all, without a word; ReleaseLibraryType releases it. A type the library already holds, named or depended on, is refused, as bringing one over it can renumber its version in work: so publish types sharing a data type together, in one call. A library open in TIA Portal is refused, as saving it would save what is pending there too. A write policy names the library by its full folder, e.g. 'global-library/C:/Libraries/Faceplates'. The library is archived to the backup registry first, then saved and closed. Answers with every type the library holds afterwards.")]
        public static ResponseLibraryTypes PublishLibraryTypes(
            [Description("globalLibraryPath: the global library file, e.g. 'C:\\Libraries\\Faceplates\\Faceplates.al21'")] string globalLibraryPath,
            [Description("typeNames: the types to publish, exactly as GetLibraryTypes names them")] string[] typeNames)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var names = (typeNames ?? Array.Empty<string>()).Select(name => (name ?? string.Empty).Trim()).ToList();
                var target = ChangeTarget.GlobalLibraryFile(globalLibraryPath);
                var backupDirectory = Backups.Allocate("PublishLibraryTypes", target);
                var request = new Governance.ChangeRequest("PublishLibraryTypes", target, $"{globalLibraryPath}: {string.Join(", ", names)}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.PublishLibraryTypes(globalLibraryPath!, names, backupDirectory), $"{names.Count} type(s) published to '{globalLibraryPath}'."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"publishing library types to '{globalLibraryPath}'");
            }
        }

        [McpServerTool(Name = "EditLibraryType"), Description("Start a new version in work of a project library type, from its newest released version; TIA Portal numbers it. For a type Openness has no class for, such as a WinCC Unified faceplate; a PLC data type or block needs a test instance, so it gets a new version from a document with CreateLibraryTypeVersionFromDocument. A type with a version in work already is refused, as TIA Portal edits one at a time. The new version holds what the released one held: a faceplate's content is still changed by hand in TIA Portal, then ReleaseLibraryType releases it, and UpdateUnifiedFaceplates moves its instances.")]
        public static ResponseLibraryTypes EditLibraryType(
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var name = (typeName ?? string.Empty).Trim();
                var target = ChangeTarget.LibraryType(name);
                var backupDirectory = Backups.Allocate("EditLibraryType", target);
                var request = new Governance.ChangeRequest("EditLibraryType", target, "a new version in work").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.EditLibraryType(name, backupDirectory), $"A new version of '{name}' is in work. Save the project to keep it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"editing library type '{typeName}'");
            }
        }

        [McpServerTool(Name = "CreatePlcTypeFromLibrary"), Description("Bring a project library PLC data type (UDT) into a program, as an instance linked to the type, in the program's root; a version named must be released, and the newest released is the default. A program already holding a type of that name, in any group, or one it depends on, is refused, as TIA Portal would answer with it and no error. Block types are not taken yet. Nothing existing changes.")]
        public static ResponseLibraryTypes CreatePlcTypeFromLibrary(
            [Description("softwarePath: full path to the PLC software, e.g. 'PLC_1'")] string softwarePath,
            [Description("typeName: the PLC data type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the released version, e.g. '0.0.1'; empty for the newest")] string version = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a number that does not parse is a mistake in the call, not a change to approve.
                var number = string.IsNullOrWhiteSpace(version) ? null : LibraryVersionNumber.Parse(version, "version");
                var name = (typeName ?? string.Empty).Trim();
                var request = new Governance.ChangeRequest("CreatePlcTypeFromLibrary", ChangeTarget.Program(softwarePath), $"{name} {version}".Trim());

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreatePlcTypeFromLibrary(softwarePath, name, number), $"'{name}' created in '{softwarePath}'. Compile the program to check it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating library type '{typeName}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "UpdateUnifiedFaceplates"), Description("Move every instance of a faceplate type on a WinCC Unified HMI to another released version, the newest by default; what each interface was given is kept. A release alone changes nothing on a screen: instances stay on the version they show until moved. A version that renames or drops a property holding a value is refused, and the instances moved back. All or nothing; each instance's version before is kept in the backup registry. Compile the HMI afterwards, as a new version may have changed its interface.")]
        public static ResponseUnifiedFaceplates UpdateUnifiedFaceplates(
            [Description("hmiPath: full path to the WinCC Unified HMI software, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("typeName: the faceplate's library type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the released version, e.g. '0.0.2'; empty for the newest")] string version = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a number that does not parse is a mistake in the call, not a change to approve.
                var number = string.IsNullOrWhiteSpace(version) ? null : LibraryVersionNumber.Parse(version, "version");
                var name = (typeName ?? string.Empty).Trim();
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("UpdateUnifiedFaceplates", target);
                var request = new Governance.ChangeRequest("UpdateUnifiedFaceplates", target, $"{name} to {(number == null ? "the newest released" : LibraryVersionNumber.Format(number))}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeMovedFaceplates(Portal.UpdateUnifiedFaceplates(hmiPath, name, number, backupDirectory), name), () => new ResponseUnifiedFaceplates(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"moving faceplates of '{typeName}' on '{hmiPath}'");
            }
        }

        private static ResponseUnifiedFaceplates DescribeMovedFaceplates(JsonObject moved, string typeName)
        {
            var count = Count(moved, "faceplates");

            return new ResponseUnifiedFaceplates(moved)
            {
                Message = $"{count} instance(s) of '{typeName}' moved to {moved["version"]}; none added. {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["faceplates"] = count
                }
            };
        }

        private static ResponseLibraryTypes DescribeLibraryChange(JsonObject content, string message)
        {
            return new ResponseLibraryTypes(content)
            {
                Message = message,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }
    }
}
