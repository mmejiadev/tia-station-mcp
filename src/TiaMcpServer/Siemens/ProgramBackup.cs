using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Exports the previous state of a PLC program's blocks and types before a write, and refuses
    /// the write when any of them could not be saved.
    /// </summary>
    /// <remarks>
    /// Blocks and types that compile are exported as SimaticML; those that do not, as SIMATIC SD
    /// documents, which TIA Portal V20 writes for an inconsistent block where SimaticML refuses
    /// (measured 2026-09-24). The backup used to be the bulk SimaticML export, which skips
    /// inconsistent blocks and only logs its failures — so in the generate, compile, fix loop it
    /// left out exactly the block about to be overwritten, and nobody was told.
    ///
    /// The group structure is mirrored below the backup directory, so a copy can be found where
    /// the block lived.
    /// </remarks>
    internal sealed class ProgramBackup
    {
        private readonly string _root;
        private readonly ILogger? _logger;
        private readonly List<string> _failures = new List<string>();

        private ProgramBackup(string root, ILogger? logger)
        {
            _root = root;
            _logger = logger;
        }

        /// <summary>Saves a PLC program's blocks and types, refusing if any could not be saved.</summary>
        /// <param name="software">The program about to be written to.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated for this change.</param>
        /// <param name="logger">Where each failure is logged with its exception.</param>
        /// <exception cref="PortalException">
        /// The directory is missing, or something could not be saved; the write must not go ahead.
        /// </exception>
        public static void Save(PlcSoftware software, string backupDirectory, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this write overwrites blocks or types of the same name");
            }

            var backup = new ProgramBackup(backupDirectory, logger);
            backup.SaveBlocks(software.BlockGroup, "Program blocks");
            backup.SaveTypes(software.TypeGroup, "PLC data types");
            backup.RequireComplete();
        }

        private void SaveBlocks(PlcBlockGroup group, string relativePath)
        {
            foreach (PlcBlock block in group.Blocks)
            {
                Attempt(block.Name, relativePath, directory => SaveBlock(block, directory));
            }

            foreach (PlcBlockUserGroup subgroup in group.Groups)
            {
                SaveBlocks(subgroup, Path.Combine(relativePath, subgroup.Name));
            }
        }

        private void SaveTypes(PlcTypeGroup group, string relativePath)
        {
            foreach (PlcType type in group.Types)
            {
                Attempt(type.Name, relativePath, directory => SaveType(type, directory));
            }

            foreach (PlcTypeUserGroup subgroup in group.Groups)
            {
                SaveTypes(subgroup, Path.Combine(relativePath, subgroup.Name));
            }
        }

        private static void SaveBlock(PlcBlock block, DirectoryInfo directory)
        {
            if (block.IsConsistent)
            {
                block.Export(XmlFile(directory, block.Name), ExportOptions.None);
                return;
            }

            RequireDocument(block.ExportAsDocuments(directory, block.Name));
        }

        private static void SaveType(PlcType type, DirectoryInfo directory)
        {
            if (type.IsConsistent)
            {
                type.Export(XmlFile(directory, type.Name), ExportOptions.None);
                return;
            }

            RequireDocument(type.ExportAsDocuments(directory, type.Name));
        }

        /// <remarks>
        /// A failure is recorded, not swallowed: RequireComplete refuses the write if there is any.
        /// One is not allowed to stop the others, so the refusal names every item that is missing.
        /// </remarks>
        private void Attempt(string name, string relativePath, Action<DirectoryInfo> save)
        {
            try
            {
                var path = Path.Combine(_root, relativePath);
                Directory.CreateDirectory(path);

                // A new DirectoryInfo, not the one CreateDirectory returns: on .NET Framework that one
                // keeps only the last folder name as its original path, and ExportAsDocuments reads
                // that and refuses it as relative (measured 2026-09-30).
                save(new DirectoryInfo(path));
            }
            catch (Exception ex)
            {
                _failures.Add($"{relativePath}/{name}: {DescribeFailure(ex)}");
                _logger?.LogError(ex, "Could not back up {Path}/{Name}", relativePath, name);
            }
        }

        private void RequireComplete()
        {
            if (_failures.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"Nothing was written: the backup of the previous state is incomplete. {_failures.Count} item(s) could not be saved: {string.Join("; ", _failures)}");
            }
        }

        /// <remarks>
        /// Openness wraps the reason: the outer message only says which method failed, and what TIA
        /// Portal objected to is in an inner exception. Without it the refusal cannot be acted on.
        /// </remarks>
        private static string DescribeFailure(Exception ex)
        {
            var messages = new List<string>();

            for (var current = ex; current != null; current = current.InnerException)
            {
                messages.Add(current.Message);
            }

            return string.Join(" -> ", messages);
        }

        private static FileInfo XmlFile(DirectoryInfo directory, string name)
        {
            return new FileInfo(Path.Combine(directory.FullName, $"{name}.xml"));
        }

        private static void RequireDocument(DocumentExportResult? result)
        {
            if (result == null || result.State != DocumentResultState.Success)
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"exporting it as a document returned {result?.State.ToString() ?? "no result"}");
            }
        }
    }
}
