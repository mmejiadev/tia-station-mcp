using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Exports a PLC program to plain text, laid out so Git produces readable diffs.
    /// </summary>
    /// <remarks>
    /// This is not the same operation as <c>ExportBlock</c>. That one writes SimaticML XML, which
    /// is faithful but enormous and unreadable in a diff. Here blocks go through
    /// <c>PlcExternalSourceSystemGroup.GenerateSource</c>
    /// to produce the actual SCL, DB or STL source, which is the point of putting a project under
    /// version control at all. The price is that blocks written in LAD, FBD or GRAPH have no text
    /// form and cannot be included; they are reported in
    /// <see cref="SnapshotResult.Unsupported"/> rather than quietly omitted.
    /// </remarks>
    public sealed class SourceSnapshotExporter
    {
        private const string BlocksFolderName = "blocks";
        private const string TypesFolderName = "types";
        private const string TagsFolderName = "tags";
        private const string TypeExtension = ".udt";
        private const string TagTableExtension = ".xml";

        private static readonly Dictionary<ProgrammingLanguage, string> SourceExtensions =
            new Dictionary<ProgrammingLanguage, string>
            {
                [ProgrammingLanguage.SCL] = ".scl",
                [ProgrammingLanguage.DB] = ".db",
                [ProgrammingLanguage.STL] = ".awl"
            };

        private readonly PlcSoftware _software;
        private readonly PlcExternalSourceSystemGroup _externalSourceGroup;
        private readonly ILogger? _logger;

        /// <summary>Creates an exporter for one PLC software container.</summary>
        /// <param name="software">The PLC software to snapshot.</param>
        /// <param name="logger">Optional logger.</param>
        public SourceSnapshotExporter(PlcSoftware software, ILogger? logger = null)
        {
            _software = software ?? throw new ArgumentNullException(nameof(software));
            _externalSourceGroup = software.ExternalSourceGroup;
            _logger = logger;
        }

        /// <summary>
        /// Writes blocks, types and tag tables under <paramref name="targetDirectory"/>, mirroring
        /// the project's group hierarchy as folders.
        /// </summary>
        /// <param name="targetDirectory">Root of the snapshot. Created if it does not exist.</param>
        /// <param name="cancellationToken">Cancels between items; a single export is not interruptible.</param>
        /// <returns>What was written and what was left out, with reasons.</returns>
        /// <exception cref="PortalException">The target directory is missing or invalid.</exception>
        public SnapshotResult ExportSnapshot(string targetDirectory, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "targetDirectory is required");
            }

            _logger?.LogInformation("Exporting source snapshot of {Software} to {TargetDirectory}...", _software.Name, targetDirectory);

            var report = new SnapshotReportBuilder(targetDirectory);

            ExportBlockGroup(_software.BlockGroup, Path.Combine(targetDirectory, BlocksFolderName), report, cancellationToken);
            ExportTypeGroup(_software.TypeGroup, Path.Combine(targetDirectory, TypesFolderName), report, cancellationToken);
            ExportTagTableGroup(_software.TagTableGroup, Path.Combine(targetDirectory, TagsFolderName), report, cancellationToken);

            var result = report.Build();

            _logger?.LogInformation(
                "Snapshot done: {Exported} written, {Unsupported} without a text form, {Inconsistent} inconsistent, {Failed} failed",
                result.Exported.Count, result.Unsupported.Count, result.Inconsistent.Count, result.Failed.Count);

            return result;
        }

        private void ExportBlockGroup(PlcBlockGroup group, string directory, SnapshotReportBuilder report, CancellationToken cancellationToken)
        {
            foreach (var block in group.Blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExportBlock(block, directory, report);
            }

            foreach (var nested in group.Groups)
            {
                ExportBlockGroup(nested, Path.Combine(directory, SnapshotFileName.For(nested.Name)), report, cancellationToken);
            }
        }

        private void ExportBlock(PlcBlock block, string directory, SnapshotReportBuilder report)
        {
            if (!SourceExtensions.TryGetValue(block.ProgrammingLanguage, out var extension))
            {
                report.AddUnsupported(block.Name, block.ProgrammingLanguage.ToString());

                return;
            }

            if (!block.IsConsistent)
            {
                report.AddInconsistent(block.Name);

                return;
            }

            var file = PrepareFile(directory, block.Name, extension, report);

            if (file == null)
            {
                return;
            }

            try
            {
                _externalSourceGroup.GenerateSource(new[] { block }, file, GenerateOptions.None);
                report.AddExported(file);
            }
            catch (Exception ex)
            {
                report.AddFailure(block.Name, ex.Message);
                _logger?.LogError(ex, "Failed to generate source for block {Block}", block.Name);
            }
        }

        private void ExportTypeGroup(PlcTypeGroup group, string directory, SnapshotReportBuilder report, CancellationToken cancellationToken)
        {
            foreach (var type in group.Types)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExportType(type, directory, report);
            }

            // The reference implementation in repos/TiaExportBlocks never recursed here, so UDTs
            // filed in user groups were silently missing from its exports.
            foreach (var nested in group.Groups)
            {
                ExportTypeGroup(nested, Path.Combine(directory, SnapshotFileName.For(nested.Name)), report, cancellationToken);
            }
        }

        private void ExportType(PlcType type, string directory, SnapshotReportBuilder report)
        {
            if (!type.IsConsistent)
            {
                report.AddInconsistent(type.Name);

                return;
            }

            var file = PrepareFile(directory, type.Name, TypeExtension, report);

            if (file == null)
            {
                return;
            }

            try
            {
                _externalSourceGroup.GenerateSource(new[] { type }, file, GenerateOptions.None);
                report.AddExported(file);
            }
            catch (Exception ex)
            {
                report.AddFailure(type.Name, ex.Message);
                _logger?.LogError(ex, "Failed to generate source for type {Type}", type.Name);
            }
        }

        private void ExportTagTableGroup(PlcTagTableGroup group, string directory, SnapshotReportBuilder report, CancellationToken cancellationToken)
        {
            foreach (var table in group.TagTables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExportTagTable(table, directory, report);
            }

            foreach (var nested in group.Groups)
            {
                ExportTagTableGroup(nested, Path.Combine(directory, SnapshotFileName.For(nested.Name)), report, cancellationToken);
            }
        }

        private void ExportTagTable(PlcTagTable table, string directory, SnapshotReportBuilder report)
        {
            var file = PrepareFile(directory, table.Name, TagTableExtension, report);

            if (file == null)
            {
                return;
            }

            try
            {
                // Tag tables have no text source form; XML is the only export TIA Portal offers.
                // It still diffs acceptably because one table is one file.
                table.Export(file, ExportOptions.WithDefaults);
                report.AddExported(file);
            }
            catch (Exception ex)
            {
                report.AddFailure(table.Name, ex.Message);
                _logger?.LogError(ex, "Failed to export tag table {Table}", table.Name);
            }
        }

        /// <remarks>
        /// Returns null when another item in this snapshot has already written to the same path.
        /// Two TIA names can map to one file name, and the delete below used to make the second
        /// item replace the first while the report claimed both were exported.
        /// </remarks>
        private static FileInfo? PrepareFile(string directory, string name, string extension, SnapshotReportBuilder report)
        {
            var file = new FileInfo(Path.Combine(directory, SnapshotFileName.For(name) + extension));

            if (!report.TryClaim(file, name))
            {
                return null;
            }

            Directory.CreateDirectory(directory);

            // Both GenerateSource and Export refuse to write over an existing file.
            if (file.Exists)
            {
                file.Delete();
            }

            return file;
        }
    }
}
