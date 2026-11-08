using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Library types of blocks and PLC data types, and their new versions, from SIMATIC SD documents.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateLibraryTypeFromDocument"), Description("Create a project library type from a SIMATIC SD document (.s7dcl) declaring a function, a function block or a PLC data type: the first version in work, and an instance linked to it in the program. A name the program or the library already holds is refused. The instance is compiled: a document that does not compile is refused and leaves nothing, as TIA Portal takes one without a word. Then ReleaseLibraryType releases it. Nothing existing changes. Save the project to keep it.")]
        public static ResponseLibraryTypes CreateLibraryTypeFromDocument(
            [Description("softwarePath: full path to the PLC software, e.g. 'PLC_1'")] string softwarePath,
            [Description("importPath: full path of the folder holding the document")] string importPath,
            [Description("documentName: the document's name without its extension; it must be the name it declares")] string documentName,
            [Description("groupPath: the block or data type group for the instance; empty for the root")] string groupPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                RequireDocumentSupport("CreateLibraryTypeFromDocument");
                var name = (documentName ?? string.Empty).Trim();
                // It writes to the program and to the library, and a policy has to allow both.
                var target = GuardedWrites.TargetToPropose(ChangeTarget.Program(softwarePath, groupPath), ChangeTarget.ProjectLibrary);
                var request = new Governance.ChangeRequest("CreateLibraryTypeFromDocument", target, $"library type {name} from {importPath}");

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreateLibraryTypeFromDocument(softwarePath, groupPath, importPath, name), $"Library type '{name}' created, its first version in work. Save the project to keep it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating a library type from document '{documentName}'");
            }
        }

        [McpServerTool(Name = "CreateLibraryTypeVersionFromDocument"), Description("Make a new version in work of a block or PLC data type library type from a SIMATIC SD document named after the type; TIA Portal numbers it. This changes the program too: its instance of the type moves onto the new version with the document's content, so the type needs an instance there. A type with a version in work is refused unless replaceInWork is true, which replaces what that version holds. The instance is compiled: a document that does not compile is refused and undone, as TIA Portal takes one without a word. The program's blocks and types, the type's versions and the instance as a document are kept in the backup registry first. Only the instance is compiled, so compile the program afterwards: what calls it may need a changed interface. Then ReleaseLibraryType releases it. Save the project to keep it.")]
        public static ResponseLibraryTypes CreateLibraryTypeVersionFromDocument(
            [Description("softwarePath: full path to the PLC software holding the type's instance, e.g. 'PLC_1'")] string softwarePath,
            [Description("typeName: the type, exactly as GetLibraryTypes names it")] string typeName,
            [Description("importPath: full path of the folder holding the document named after the type")] string importPath,
            [Description("replaceInWork: true to replace what the version in work holds rather than make a new version")] bool replaceInWork = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                RequireDocumentSupport("CreateLibraryTypeVersionFromDocument");
                var name = (typeName ?? string.Empty).Trim();
                // It writes to the program and to the type, and a policy has to allow both.
                var target = GuardedWrites.TargetToPropose(ChangeTarget.Program(softwarePath), ChangeTarget.LibraryType(name));
                var backupDirectory = Backups.Allocate("CreateLibraryTypeVersionFromDocument", target);
                var request = new Governance.ChangeRequest("CreateLibraryTypeVersionFromDocument", target, $"{(replaceInWork ? "replacing the version in work of" : "a new version of")} {name} from {importPath}").WithBackup(backupDirectory);
                var documents = new DocumentImportRequest(softwarePath, string.Empty, importPath, backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeLibraryChange(Portal.CreateLibraryTypeVersionFromDocument(documents, name, replaceInWork), $"'{name}' has its version in work from the document, and its instance in '{softwarePath}' holds it. Compile the program, as what calls it may need its new interface, and save the project to keep it."), () => new ResponseLibraryTypes(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"making a version of library type '{typeName}' from a document");
            }
        }
    }
}
