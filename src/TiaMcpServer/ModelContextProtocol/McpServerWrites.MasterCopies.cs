using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>Master copies of the project library: folders, making one, and creating from one. Reading them is <c>GetMasterCopies</c>.</remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateMasterCopyFolder"), Description("Create a master copy folder in the open project's library, e.g. 'Motors' or 'Motors/Pumps'; the folders above it must exist. A folder already there is refused. The library's master copies before are listed in the backup registry. Save the project to keep it.")]
        public static ResponseMasterCopies CreateMasterCopyFolder(
            [Description("folderPath: the new folder, e.g. 'Motors/Pumps'")] string folderPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var backupDirectory = Backups.Allocate("CreateMasterCopyFolder", ChangeTarget.ProjectLibrary);
                var request = new Governance.ChangeRequest("CreateMasterCopyFolder", ChangeTarget.ProjectLibrary, $"folder {folderPath}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeMasterCopyChange(Portal.CreateMasterCopyFolder(folderPath, backupDirectory), $"Master copy folder '{folderPath}' created. Save the project to keep it."), () => new ResponseMasterCopies(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating master copy folder '{folderPath}'");
            }
        }

        [McpServerTool(Name = "CreateMasterCopy"), Description("Make a master copy in the open project's library from a block, a tag table or a PLC data type (UDT) of a program, so it can be created again in any program with CreateFromMasterCopy. The object is copied as it stands and nothing in the program changes. The path is exact, never a name pattern. A folder already holding a master copy of that name is refused. The library's master copies before are listed in the backup registry. Save the project to keep it.")]
        public static ResponseMasterCopies CreateMasterCopy(
            [Description("softwarePath: full path to the PLC software, e.g. 'PLC_1'")] string softwarePath,
            [Description("kind: what the object is, 'block', 'tagTable' or 'plcType'")] string kind,
            [Description("path: the object's full path in the program, e.g. 'Cell/FB_Motor' or 'Default tag table'")] string path,
            [Description("folderPath: the master copy folder to put it in, e.g. 'Motors'; empty for the top folder")] string folderPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a kind that does not parse is a mistake in the call, not a change to approve.
                var source = new MasterCopySource(MasterCopyRules.ParseKind(kind), (path ?? string.Empty).Trim());
                var backupDirectory = Backups.Allocate("CreateMasterCopy", ChangeTarget.ProjectLibrary);
                var request = new Governance.ChangeRequest("CreateMasterCopy", ChangeTarget.ProjectLibrary, $"{kind} {softwarePath}/{source.Path} into '{folderPath}'").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeMasterCopyChange(Portal.CreateMasterCopy(softwarePath, source, folderPath, backupDirectory), $"Master copy of '{source.Path}' created. Save the project to keep it."), () => new ResponseMasterCopies(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"making a master copy of '{path}' from '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "CreateFromMasterCopy"), Description("Create a block, a tag table or a PLC data type in a program from a master copy of the open project's library; what it is created as is read from the master copy, which must hold exactly one of them. A program already holding a block, data type or tag table of that name, in any group, is refused, as TIA Portal would otherwise rename the new one without a word; so is a tag table bringing a tag or constant the program already holds, which TIA Portal renames too (the table is deleted again). The group must exist. The program's blocks, types and tag tables are exported to the backup registry first. Compile the program afterwards.")]
        public static ResponseMasterCopies CreateFromMasterCopy(
            [Description("softwarePath: full path to the PLC software, e.g. 'PLC_1'")] string softwarePath,
            [Description("masterCopyPath: the master copy, e.g. 'Motors/FB_Motor', as GetMasterCopies gives its path")] string masterCopyPath,
            [Description("groupPath: the group in the program to create it in, e.g. 'Cell'; empty for the root")] string groupPath = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath, groupPath);
                var backupDirectory = Backups.Allocate("CreateFromMasterCopy", target);
                var request = new Governance.ChangeRequest("CreateFromMasterCopy", target, $"from master copy {masterCopyPath}").WithBackup(backupDirectory);

                return GuardedTool.Run(GuardedWrites, request, () => DescribeCreatedFromMasterCopy(Portal.CreateFromMasterCopy(softwarePath, masterCopyPath, groupPath, backupDirectory)), () => new ResponseMasterCopies(new JsonObject()));
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"creating '{masterCopyPath}' in '{softwarePath}'");
            }
        }

        private static ResponseMasterCopies DescribeCreatedFromMasterCopy(JsonObject created)
        {
            return DescribeMasterCopyChange(created, $"{created["kind"]} '{created["name"]}' created from master copy '{created["masterCopy"]}'. Compile the program to check it.");
        }

        private static ResponseMasterCopies DescribeMasterCopyChange(JsonObject content, string message)
        {
            return new ResponseMasterCopies(content)
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
