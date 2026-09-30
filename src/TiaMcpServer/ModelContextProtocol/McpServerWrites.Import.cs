using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Importing a block or a type from SimaticML.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ImportBlock"), Description("Import a block file to plc software. A block of the same name is replaced, so the program's blocks and types are exported to the backup registry first; call ListBackups to find that copy. If any of them cannot be saved, nothing is imported.")]
        public static ResponseImportBlock ImportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the block")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the block")] string importPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath, groupPath);
                var backupDirectory = Backups.Allocate("ImportBlock", target);
                var request = new Governance.ChangeRequest("ImportBlock", target, importPath)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Portal.ImportBlock(softwarePath, groupPath, importPath, backupDirectory);

                        return new ResponseImportBlock
                        {
                            Message = $"Block imported from '{importPath}' to '{groupPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseImportBlock());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"importing block from '{importPath}' to '{groupPath}'");
            }
        }

        [McpServerTool(Name = "ImportType"), Description("Import a type from file into the plc software. A type of the same name is replaced, so the program's blocks and types are exported to the backup registry first; call ListBackups to find that copy. If any of them cannot be saved, nothing is imported.")]
        public static ResponseImportType ImportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the type")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the type")] string importPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath, groupPath);
                var backupDirectory = Backups.Allocate("ImportType", target);
                var request = new Governance.ChangeRequest("ImportType", target, importPath)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Portal.ImportType(softwarePath, groupPath, importPath, backupDirectory);

                        return new ResponseImportType
                        {
                            Message = $"Type imported from '{importPath}' to '{groupPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseImportType());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"importing type from '{importPath}' to '{groupPath}'");
            }
        }
    }
}
