using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The project as a whole: what is open, opening one, its tree, its snapshots and backups.
    ///
    /// SaveProjectAs is private and lives here rather than with the writes it serves: it is the
    /// body of a guarded write, and McpServerWrites holds the guard that calls it.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetProject"), Description("Get open local project/session")]
        public static ResponseGetProjects GetProjects()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = new List<TiaMcpServer.Siemens.ObjectDescription>(Portal.GetProjects());

                list.AddRange(Portal.GetSessions());

                var responseList = new List<ResponseProjectInfo>();
                foreach (var project in list)
                {
                    responseList.Add(new ResponseProjectInfo
                    {
                        Name = project.Name,
                        Attributes = project.Attributes
                    });
                }

                return new ResponseGetProjects
                {
                    Message = "Open projects and sessions retrieved",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving open projects: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "OpenProject"), Description("Open a TIA-Portal local project/session")]
        public static ResponseOpenProject OpenProject(
            [Description("path: defines the path where to the project/session")] string path)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                Portal.CloseProject();

                // get project extension
                string extension = Path.GetExtension(path).ToLowerInvariant();

                // use regex to check if extension is .ap\d+ or .als\d+
                if (!Regex.IsMatch(extension, @"^\.ap\d+$") &&
                    !Regex.IsMatch(extension, @"^\.als\d+$"))
                {
                    throw new McpException("Invalid project file extension. Use .apXX for projects or .alsXX for sessions, where XX=18,19,20,....", McpErrorCode.InvalidParams);
                }

                bool success = false;

                if (extension.StartsWith(".ap"))
                {
                    success = Portal.OpenProject(path);
                }
                if (extension.StartsWith(".als"))
                {
                    success = Portal.OpenSession(path);
                }

                if (success)
                {
                    return new ResponseOpenProject
                    {
                        Message = $"Project '{path}' opened",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed to open project '{path}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening project '{path}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "RetrieveProject"), Description("Retrieve a TIA-Portal project archive (.zapXX) into a directory and open it")]
        public static ResponseRetrieveProject RetrieveProject(
            [Description("archivePath: full path of the .zapXX archive to retrieve")] string archivePath,
            [Description("targetDirectory: directory to extract into; the call is refused if the project folder already exists")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var projectPath = Portal.RetrieveProject(archivePath, targetDirectory);

                return new ResponseRetrieveProject(projectPath)
                {
                    Message = $"Archive '{archivePath}' retrieved to '{projectPath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to retrieve archive '{archivePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving archive '{archivePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetProjectTree"), Description("Get project structure as a tree view on current local project/session")]
        public static ResponseProjectTree GetProjectTree()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var tree = Portal.GetProjectTree();

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseProjectTree
                    {
                        Message = "Project tree retrieved",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed retrieving project tree", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving project tree: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static ResponseSaveAsProject SaveProjectAs(string newProjectPath)
        {
            if (!Portal.SaveAsProject(newProjectPath))
            {
                throw new McpException($"Failed saving local project as '{newProjectPath}'", McpErrorCode.InternalError);
            }

            return new ResponseSaveAsProject
            {
                Message = $"Local project saved as '{newProjectPath}'",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true
                }
            };
        }

        [McpServerTool(Name = "ExportSourceSnapshot"), Description("Export a PLC program to plain text (SCL/DB/AWL sources, UDTs and tag tables) laid out for version control. Blocks written in LAD, FBD or GRAPH have no text form and are reported as unsupported instead of being exported.")]
        public static ResponseExportSnapshot ExportSourceSnapshot(
            [Description("softwarePath: full path in the project structure to the plc software, e.g. 'Group1/PLC_1'")] string softwarePath,
            [Description("targetDirectory: root directory of the snapshot; blocks, types and tags are written into subfolders")] string targetDirectory)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var snapshot = Portal.ExportSourceSnapshot(softwarePath, targetDirectory);

                return new ResponseExportSnapshot(snapshot.Exported, snapshot.Inconsistent, snapshot.Unsupported, snapshot.Failed)
                {
                    Message = BuildSnapshotMessage(snapshot),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["isComplete"] = snapshot.IsComplete
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to export a snapshot of '{softwarePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting a snapshot of '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static string BuildSnapshotMessage(TiaMcpServer.Siemens.SnapshotResult snapshot)
        {
            var message = $"Snapshot written: {snapshot.Exported.Count} file(s)";

            // Say out loud when the snapshot is partial. A caller that assumes otherwise would
            // treat it as a backup of the whole program, which it is not.
            if (snapshot.Unsupported.Count > 0)
            {
                message += $"; {snapshot.Unsupported.Count} block(s) have no text representation and were left out";
            }

            if (snapshot.Inconsistent.Count > 0)
            {
                message += $"; {snapshot.Inconsistent.Count} inconsistent item(s) skipped, compile the software and export again";
            }

            if (snapshot.Failed.Count > 0)
            {
                message += $"; {snapshot.Failed.Count} item(s) failed";
            }

            return message;
        }

        [McpServerTool(Name = "ListBackups"), Description("List every copy of previous state the server saved before overwriting something. A write tool takes one automatically; this is how the copy is found again. An entry with fileCount 0 is a change that was refused or failed before exporting, so there is nothing in it.")]
        public static ResponseBackups ListBackups()
        {
            try
            {
                var backups = Backups.List();
                var items = backups.Select(ToResponse).ToList();
                var empty = backups.Count(backup => backup.IsEmpty);

                return new ResponseBackups(items)
                {
                    Message = items.Count == 0
                        ? "No backups have been taken yet."
                        : $"{items.Count} backup(s), newest first; {empty} hold nothing.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = items.Count,
                        ["emptyCount"] = empty
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to list the backups");
            }
        }

        private static ResponseBackup ToResponse(Governance.BackupRecord backup)
        {
            return new ResponseBackup(
                backup.Path,
                backup.Tool,
                backup.Target,
                backup.TakenAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                backup.FileCount);
        }
    }
}
