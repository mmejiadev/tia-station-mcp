using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <remarks>Changing some members of a global data block; see <see cref="DbMemberXml"/>.</remarks>
    public partial class Portal
    {
        private const string DbEditFolder = "edit-db";
        private const int ErrorsQuoted = 3;

        /// <summary>Changes some members of a global data block, leaving the others as they are.</summary>
        /// <param name="softwarePath">Full path to the PLC software, e.g. <c>PLC_1</c>.</param>
        /// <param name="edit">The members to remove, change in place and add.</param>
        /// <param name="backupDirectory">Where the program's blocks and types are exported first, and the block before and after the edit.</param>
        /// <returns><c>{ block, removed, set, added, compile, files }</c>: what was done, and the compile before and after it.</returns>
        /// <exception cref="PortalException">
        /// The block is missing, is not a global data block or is inconsistent, a member is missing or in use, TIA Portal refused
        /// the edited block, or the compile after it finds more errors than before - the block is then put back as it was.
        /// </exception>
        /// <remarks>
        /// Measured 2026-11-22 on V21: the block imported again is a new object, inconsistent until the software is compiled, and
        /// removing a member a block reads is not refused by the import, only by the compile. So the compile decides.
        /// </remarks>
        public JsonObject EditDbMembers(string softwarePath, DbMemberEdit edit, string backupDirectory)
        {
            _logger?.LogInformation("Editing members of {Block} in {SoftwarePath}...", edit?.Block, softwarePath);

            try
            {
                var requested = edit ?? throw new PortalException(PortalErrorCode.InvalidParams, "An edit is required");
                var software = RequireOfflineSoftware(softwarePath);
                var block = RequireConsistentGlobalDb(softwarePath, requested.Block);
                var group = block.Parent as PlcBlockGroup ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{requested.Block}' is in no block group");

                ProgramBackup.Save(software, backupDirectory, _logger);
                var files = WriteDbEditFiles(block, requested, backupDirectory);
                var before = CompileSoftware(softwarePath).ErrorCount;
                var after = ReplaceDb(softwarePath, group, files, (before, backupDirectory));

                return DescribeDbEdit(requested, (before, after), files);
            }
            catch (Exception ex)
            {
                throw DecorateDbFailure(ex, softwarePath, edit?.Block ?? string.Empty, "EditDbMembers");
            }
        }

        private GlobalDB RequireConsistentGlobalDb(string softwarePath, string blockPath)
        {
            var block = FindBlock(softwarePath, blockPath) ?? throw new PortalException(PortalErrorCode.NotFound, $"Block not found: '{blockPath}' in {softwarePath}");
            var database = block as GlobalDB ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{blockPath}' is not a global data block: its members are not edited here");

            return database.IsConsistent
                ? database
                : throw new PortalException(PortalErrorCode.InvalidState, $"'{blockPath}' is inconsistent, and TIA Portal exports no inconsistent block: compile the software first");
        }

        /// <remarks>The block as exported, and as the edit leaves it, kept in the backup folder: the first is what an undo imports.</remarks>
        private static (FileInfo Original, FileInfo Changed) WriteDbEditFiles(GlobalDB block, DbMemberEdit edit, string backupDirectory)
        {
            var folder = Path.Combine(backupDirectory, DbEditFolder);
            var original = new FileInfo(Path.Combine(folder, "original", $"{block.Name}.xml"));
            var changed = new FileInfo(Path.Combine(folder, "changed", $"{block.Name}.xml"));

            Directory.CreateDirectory(original.DirectoryName!);
            Directory.CreateDirectory(changed.DirectoryName!);
            block.Export(original, ExportOptions.None);
            File.WriteAllText(changed.FullName, DbMemberXml.Apply(File.ReadAllText(original.FullName, Encoding.UTF8), edit), new UTF8Encoding(false));

            return (original, changed);
        }

        /// <returns>The errors of the compile after the edit.</returns>
        private int ReplaceDb(string softwarePath, PlcBlockGroup group, (FileInfo Original, FileInfo Changed) files, (int ErrorsBefore, string BackupDirectory) previous)
        {
            try
            {
                ImportDb(group, files.Changed);
            }
            catch (Exception refused)
            {
                RestoreDb(softwarePath, group, files.Original, previous.BackupDirectory);
                throw new PortalException(PortalErrorCode.WriteFailed, $"Nothing was changed: TIA Portal refused the edited block ({refused.Message.Trim()}). The block is back as it was.", null, refused);
            }

            var report = CompileSoftware(softwarePath);
            if (report.ErrorCount <= previous.ErrorsBefore)
            {
                return report.ErrorCount;
            }

            RestoreDb(softwarePath, group, files.Original, previous.BackupDirectory);
            var quoted = string.Join(" | ", report.Errors.Take(ErrorsQuoted).Select(error => error.Description));

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"Nothing was changed: with the edit the software has {report.ErrorCount} errors, against {previous.ErrorsBefore} before ({quoted}). The block is back as it was.");
        }

        private static void ImportDb(PlcBlockGroup group, FileInfo file)
        {
            var imported = group.Blocks.Import(file, ImportOptions.Override);

            if (imported == null || imported.Count == 0)
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal imported no block from '{file.FullName}'");
            }
        }

        /// <remarks>Imports the block as exported and compiles, which makes it, and what reads it, consistent again.</remarks>
        private void RestoreDb(string softwarePath, PlcBlockGroup group, FileInfo original, string backupDirectory)
        {
            try
            {
                ImportDb(group, original);
                CompileSoftware(softwarePath);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"The edit failed, and the block could not be put back ({failure.Message.Trim()}). The program as it was is in '{backupDirectory}', the block alone in '{original.FullName}'.",
                    null,
                    failure);
            }
        }

        private static JsonObject DescribeDbEdit(DbMemberEdit edit, (int Before, int After) errors, (FileInfo Original, FileInfo Changed) files)
        {
            return new JsonObject
            {
                ["block"] = edit.Block,
                ["removed"] = new JsonArray(edit.Remove.Select(name => (JsonNode)name).ToArray()),
                ["set"] = new JsonArray(edit.Set.Select(row => (JsonNode)row.Name).ToArray()),
                ["added"] = new JsonArray(edit.Add.Select(row => (JsonNode)row.Name).ToArray()),
                ["compile"] = new JsonObject { ["errorsBefore"] = errors.Before, ["errorsAfter"] = errors.After },
                ["files"] = new JsonObject { ["original"] = files.Original.FullName, ["changed"] = files.Changed.FullName }
            };
        }

        private PortalException DecorateDbFailure(Exception ex, string softwarePath, string blockPath, string operation)
        {
            var pex = ForgetEndedSession(ex, operation)
                ?? ex as PortalException
                ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["softwarePath"] = softwarePath;
            pex.Data["blockPath"] = blockPath;

            _logger?.LogError(pex, "{Operation} failed for {SoftwarePath} {BlockPath}", operation, softwarePath, blockPath);
            return pex;
        }
    }
}
