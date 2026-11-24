using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>A program's instances of block and PLC data type library types: a block created from a type, and instances moved to another version.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreatePlcBlockFromLibrary"), Description("Create a block in a program from a project library block type (a function or function block type), as an instance linked to the type, in the group given; a version named must be released, and the newest released is the default. A program already holding a block or data type of its name, or of a type it depends on, is refused, as TIA Portal would answer with the one there or create 'NAME_1' without a word. The block is inconsistent until the program is compiled. CopyLibraryTypes brings a type from a global library first. Nothing existing changes.")]
        public static ResponseLibraryTypes CreatePlcBlockFromLibrary(
            [Description("softwarePath: full path to the PLC software, e.g. 'PLC_1'")] string softwarePath,
            [Description("typeName: the block type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the released version, e.g. '0.0.1'; empty for the newest")] string version = "",
            [Description("groupPath: the block group to create it in, e.g. 'Cell'; empty for the root")] string groupPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                // Read before the guard: a number that does not parse is a mistake in the call, not a change to approve.
                var number = string.IsNullOrWhiteSpace(version) ? null : LibraryVersionNumber.Parse(version, "version");
                var name = (typeName ?? string.Empty).Trim();
                var request = new Governance.ChangeRequest("CreatePlcBlockFromLibrary", ChangeTarget.Program(softwarePath, groupPath), $"{name} {version}".Trim());

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreatePlcBlockFromLibrary(softwarePath, name, number, groupPath), $"'{name}' created in '{softwarePath}'. Compile the program to check it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating block type '{typeName}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "UpdatePlcLibraryInstances"), Description("Move a program's instances of a block or PLC data type library type to another released version, the newest by default: after ReleaseLibraryType, instances keep the version they show until moved, and GetLibraryTypeUsage lists them as out of date. Each instance is deleted and created again from that version in its group, as TIA Portal V21's own update was measured to remove instances from the program instead of updating them. The program is compiled before and after; when it has more errors after - what uses the type does not fit the new version - every instance is put back and the call is refused. A function block is refused, as what this does to its instance data blocks was not measured; so is an instance on a version in work, which is what that version is edited through (ReleaseLibraryType first). The program's blocks, types and tag tables are exported to the backup registry first. Save the project to keep it.")]
        public static ResponseLibraryTypes UpdatePlcLibraryInstances(
            [Description("softwarePath: full path to the PLC software, e.g. 'PLC_1'")] string softwarePath,
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("version: the released version, e.g. '0.0.2'; empty for the newest")] string version = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                // Read before the guard: a number that does not parse is a mistake in the call, not a change to approve.
                var number = string.IsNullOrWhiteSpace(version) ? null : LibraryVersionNumber.Parse(version, "version");
                var name = (typeName ?? string.Empty).Trim();
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("UpdatePlcLibraryInstances", target);
                var request = new Governance.ChangeRequest("UpdatePlcLibraryInstances", target, $"{name} to {(number == null ? "the newest released" : LibraryVersionNumber.Format(number))}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeMovedInstances(Portal.UpdatePlcLibraryInstances(softwarePath, name, number, backupDirectory), softwarePath), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"moving the instances of '{typeName}' in '{softwarePath}'");
            }
        }

        private static ResponseLibraryTypes DescribeMovedInstances(JsonObject moved, string softwarePath)
        {
            var count = Count(moved, "moved");

            return DescribeLibraryChange(moved, count == 0
                ? $"Every instance of '{moved["type"]}' in '{softwarePath}' already shows {moved["version"]}; nothing was changed."
                : $"{count} instance(s) of '{moved["type"]}' in '{softwarePath}' moved to {moved["version"]}, and the program compiles with no more errors than before. Save the project to keep it.");
        }
    }
}
