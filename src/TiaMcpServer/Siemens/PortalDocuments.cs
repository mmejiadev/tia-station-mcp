using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// SIMATIC SD documents: the .s7dcl and .s7res pair TIA Portal V20 reads and writes. Exporting
    /// them is here; importing them is in PortalDocumentImport.
    ///
    /// Its own file because it is its own feature with its own trap: importing a LAD block from
    /// a document needs the accompanying .s7res carrying en-US tags, and without it the import
    /// fails with an error that names neither the file nor the language. That is an Openness
    /// limitation, it is documented in CLAUDE.md, and it belongs next to the code it bites.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>SIMATIC SD documents arrived with TIA Portal V20; older versions have no API for them.</summary>
        private const int FirstTiaVersionWithDocuments = 20;

        /// <summary>
        /// Exports every block whose name matches as SIMATIC SD documents (.s7dcl/.s7res).
        /// </summary>
        /// <remarks>
        /// Inconsistent blocks are exported too. SimaticML export refuses them, but SIMATIC SD export
        /// does not (measured on TIA Portal V20, 2026-09-24), and a document is the only way to read a
        /// block that does not compile. The caller learns which ones they are from IsConsistent in
        /// the result, never by their absence. A block that fails to export is left out and named in
        /// the report's failures; the others are still exported.
        /// </remarks>
        /// <param name="request">What to export and where to.</param>
        /// <param name="progress">
        /// Told how many of the selected items have been processed, after each one; null for none.
        /// </param>
        /// <param name="cancellationToken">Checked before each item; the files already written stay.</param>
        /// <returns>The blocks exported and the failures met.</returns>
        /// <exception cref="PortalException">
        /// TIA Portal is older than V20, no project is open, there is no PLC software at the path, or
        /// the filter is not valid.
        /// </exception>
        /// <exception cref="OperationCanceledException">The export was cancelled.</exception>
        public ExportReport<BlockDescription> ExportBlocksAsDocuments(BulkExportRequest request, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Exporting blocks as documents...");

            RequireDocumentSupport();
            RequireOfflineSoftware(request.SoftwarePath);

            var selected = FindBlocks(request.SoftwarePath, request.RegexName);
            var exported = new List<PlcBlock>();
            var failures = new List<string>();

            for (var index = 0; index < selected.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryExportBlockAsDocument(selected[index], request.ExportPath, request.PreservePath, failures))
                {
                    exported.Add(selected[index]);
                }

                progress?.Report(index + 1);
            }

            LogBulkExport("ExportBlocksAsDocuments", exported.Count, failures);
            return new ExportReport<BlockDescription>(DescribeBlocks(exported), failures);
        }

        private bool TryExportBlockAsDocument(PlcBlock block, string exportPath, bool preservePath, List<string> failures)
        {
            if (!block.IsConsistent)
            {
                _logger?.LogInformation($"Exporting inconsistent block {block.Name} as it stands");
            }

            var directory = DocumentDirectory(block, exportPath, preservePath);

            if (!TryCreateDirectory(directory, block.Name, failures))
            {
                return false;
            }

            DeleteStaleDocuments(directory, block.Name, failures);
            var result = ExportDocument(block, directory, failures);

            if (result == null)
            {
                return false;
            }

            if (result.State != DocumentResultState.Success)
            {
                failures.Add($"{block.Name}: result state {result.State}");
                return false;
            }

            return true;
        }

        private string DocumentDirectory(PlcBlock block, string exportPath, bool preservePath)
        {
            if (!preservePath || !(block.Parent is PlcBlockGroup parentGroup))
            {
                return exportPath;
            }

            var groupPath = GetPlcBlockGroupPath(parentGroup);
            return string.IsNullOrWhiteSpace(groupPath) ? exportPath : Path.Combine(exportPath, groupPath.Replace('/', '\\'));
        }

        private bool TryCreateDirectory(string directory, string blockName, List<string> failures)
        {
            try
            {
                Directory.CreateDirectory(directory);
                return true;
            }
            catch (Exception ex)
            {
                failures.Add($"{blockName}: cannot create directory '{directory}' ({ex.Message})");
                _logger?.LogError(ex, $"Directory creation failed for {directory}");
                return false;
            }
        }

        /// <remarks>
        /// A document left from an earlier export is removed first, so a failed export cannot leave
        /// the old one behind looking current. A file that cannot be removed is reported and the
        /// export is attempted anyway, since it may overwrite it.
        /// </remarks>
        private void DeleteStaleDocuments(string directory, string blockName, List<string> failures)
        {
            foreach (var file in new[] { Path.Combine(directory, $"{blockName}.s7dcl"), Path.Combine(directory, $"{blockName}.s7res") })
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    failures.Add($"{blockName}: cannot delete existing '{Path.GetFileName(file)}' ({ex.Message})");
                    _logger?.LogError(ex, $"Failed deleting existing file {file}");
                }
            }
        }

        /// <remarks>
        /// One block failing to export must not stop the others, so each failure is recorded rather
        /// than thrown; the log keeps the exception.
        /// </remarks>
        private DocumentExportResult? ExportDocument(PlcBlock block, string directory, List<string> failures)
        {
            try
            {
                var result = block.ExportAsDocuments(new DirectoryInfo(directory), block.Name);

                if (result == null)
                {
                    failures.Add($"{block.Name}: no result returned");
                }

                return result;
            }
            catch (EngineeringNotSupportedException ex)
            {
                failures.Add($"{block.Name}: not supported ({ex.Message})");
                _logger?.LogWarning(ex, $"EngineeringNotSupported exporting {block.Name}");
            }
            catch (LicenseNotFoundException ex)
            {
                failures.Add($"{block.Name}: license not found ({ex.Message})");
                _logger?.LogError(ex, $"License issue exporting {block.Name}");
            }
            catch (Exception ex)
            {
                failures.Add($"{block.Name}: export threw ({ex.Message})");
                _logger?.LogError(ex, $"ExportAsDocuments failed for {block.Name}");
            }

            return null;
        }

        /// <summary>Exports one block as SIMATIC SD documents (.s7dcl/.s7res).</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="blockPath">Full path to the block, for example <c>Group/Subgroup/Name</c>.</param>
        /// <param name="exportPath">Directory the documents are written to.</param>
        /// <param name="preservePath">Mirror the block's group below the export directory, as the bulk export does.</param>
        /// <remarks>
        /// Exported even when the block does not compile, like the bulk export. It goes through the
        /// same per-block routine, so the two write to the same place and fail the same way. It used
        /// to answer false for a block it did not find, and the tool could only say "failed".
        /// </remarks>
        /// <exception cref="PortalException">
        /// TIA Portal is older than V20, no project is open, the software or the block does not exist,
        /// or the documents could not be written; the message says which.
        /// </exception>
        public void ExportAsDocuments(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block as documents by path: {blockPath}");

            try
            {
                RequireDocumentSupport();
                RequireOfflineSoftware(softwarePath);

                var block = FindBlock(softwarePath, blockPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Block not found: {blockPath}");
                var failures = new List<string>();

                if (!TryExportBlockAsDocument(block, exportPath, preservePath, failures))
                {
                    throw new PortalException(PortalErrorCode.ExportFailed, $"Exporting '{blockPath}' as documents failed: {string.Join("; ", failures)}");
                }
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Export as documents failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportAsDocuments failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
        }

        /// <remarks>
        /// SIMATIC SD documents arrived with TIA Portal V20; older versions have no API for them, and
        /// asking one fails with an error that does not say so.
        /// </remarks>
        private static void RequireDocumentSupport()
        {
            if (Engineering.TiaMajorVersion < FirstTiaVersionWithDocuments)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"SIMATIC SD documents require TIA Portal V{FirstTiaVersionWithDocuments} or newer");
            }
        }
    }
}
