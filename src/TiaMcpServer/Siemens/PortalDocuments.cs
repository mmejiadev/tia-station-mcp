using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// SIMATIC SD documents: the .s7dcl and .s7res pair TIA Portal V20 reads and writes.
    ///
    /// Its own file because it is its own feature with its own trap: importing a LAD block from
    /// a document needs the accompanying .s7res carrying en-US tags, and without it the import
    /// fails with an error that names neither the file nor the language. That is an Openness
    /// limitation, it is documented in CLAUDE.md, and it belongs next to the code it bites.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Exports every block whose name matches as SIMATIC SD documents (.s7dcl/.s7res).
        /// </summary>
        /// <remarks>
        /// Inconsistent blocks are exported too. SimaticML export refuses them, but SIMATIC SD export
        /// does not (measured on TIA Portal V20, 2026-09-24), and a document is the only way to read a
        /// block that does not compile. The caller learns which ones they are from IsConsistent in
        /// the result, never by their absence. A block that fails to export is left out and logged;
        /// the others are still exported.
        /// </remarks>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="exportPath">Directory the documents are written to.</param>
        /// <param name="regexName">Name or regular expression selecting the blocks; empty for all.</param>
        /// <param name="preservePath">Mirror the block group structure below the export directory.</param>
        /// <returns>The blocks exported, or null when no project is open or TIA Portal is older than V20.</returns>
        public IReadOnlyList<BlockDescription>? ExportBlocksAsDocuments(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting blocks as documents...");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ExportBlocksAsDocuments is only supported on TIA Portal V20 or newer");
                return null;
            }

            var blocks = FindBlocksOrNone(softwarePath, regexName);
            var exported = new List<PlcBlock>();
            var failures = new List<string>();

            foreach (var block in blocks)
            {
                if (TryExportBlockAsDocument(block, exportPath, preservePath, failures))
                {
                    exported.Add(block);
                }
            }

            LogDocumentExport(exported.Count, failures, blocks.Length);
            return DescribeBlocks(exported);
        }

        private PlcBlock[] FindBlocksOrNone(string softwarePath, string regexName)
        {
            try
            {
                return FindBlocks(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to retrieve block list for {softwarePath}");
                return Array.Empty<PlcBlock>();
            }
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

        private void LogDocumentExport(int exportedCount, IReadOnlyList<string> failures, int total)
        {
            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportBlocksAsDocuments completed with {failures.Count} failures out of {total}. First failure: {failures[0]}");
                return;
            }

            _logger?.LogInformation($"ExportBlocksAsDocuments completed successfully. Exported {exportedCount} blocks.");
        }

        public bool ExportAsDocuments(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block as documents by path: {blockPath}");
            var success = false;
            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "ExportAsDocuments requires TIA Portal V20 or newer");
                }

                
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    if (plcSoftware != null)
                    {
                        // Export code blocks as documents
                        // https://docs.tia.siemens.cloud/r/en-us/v20/creating-and-managing-blocks/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500

                        var target = ProjectPath.Parse(blockPath);
                        var groupPath = target.Parent;
                        var blockName = target.Name;

                        var group = GetPlcBlockGroupByPath(softwarePath, groupPath);

                        // join exportPath and groupPath
                        if (!Directory.Exists(exportPath))
                        {
                            Directory.CreateDirectory(exportPath);
                        }

                        if (preservePath && !string.IsNullOrEmpty(groupPath))
                        {
                            exportPath = Path.Combine(exportPath, groupPath);

                            if (!Directory.Exists(exportPath))
                            {
                                Directory.CreateDirectory(exportPath);
                            }
                        }

                        try
                        {
                            // delete files s7dcl/s7res if already exists
                            var blockFiles7dclPath = Path.Combine(exportPath, $"{blockName}.s7dcl");
                            if (File.Exists(blockFiles7dclPath))
                            {
                                File.Delete(blockFiles7dclPath);
                            }
                            var blockFiles7resPath = Path.Combine(exportPath, $"{blockName}.s7res");
                            if (File.Exists(blockFiles7resPath))
                            {
                                File.Delete(blockFiles7resPath);
                            }

                            var result = group?.Blocks.Find(blockName)?.ExportAsDocuments(new DirectoryInfo(exportPath), blockName);

                            if (result != null && result.State == DocumentResultState.Success)
                            {
                                success = true;
                            }
                        }
                        catch (EngineeringNotSupportedException ex)
                        {
                            // The export or import of blocks with mixed programming languages is not possible
                            throw new PortalException(PortalErrorCode.ExportFailed, $"EngineeringNotSupportedException at block '{blockName}'. {ex.Message}", null, ex);
                        }
                        catch (Exception ex)
                        {
                            throw new PortalException(PortalErrorCode.ExportFailed, $"Exception at block '{blockName}'. {ex.Message}", null, ex);
                        }

                    }

                }


            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportAsDocuments failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
            return success;
        }

        public IReadOnlyList<BlockDescription>? ImportBlocksFromDocuments(string softwarePath, string groupPath, string importPath, string regexName, string option, bool preservePath = false)
        {
            _logger?.LogInformation($"Importing blocks from documents in {importPath} with regex '{regexName}'");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportBlocksFromDocuments is only supported on TIA Portal V20 or newer");
                return null;
            }

            var importOption = ImportDocumentOption.Parse(option);

            var imported = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    var dir = new DirectoryInfo(importPath);
                    if (!dir.Exists)
                    {
                        _logger?.LogWarning($"Import directory does not exist: {importPath}");
                        return DescribeBlocks(imported);
                    }

                    var filter = NameFilter.Parse(regexName);

                    // Consider .s7dcl as the primary index; .s7res is optional supplemental
                    var files = dir.GetFiles("*.s7dcl", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file.Name);
                        if (!filter.Matches(name))
                        {
                            continue;
                        }

                        try
                        {
                            var result = (group != null)
                                ? group.Blocks.ImportFromDocuments(dir, name, importOption)
                                : plcSoftware.BlockGroup.Blocks.ImportFromDocuments(dir, name, importOption);

                            if (result != null && result.State == DocumentResultState.Success && result.ImportedPlcBlocks != null)
                            {
                                foreach (var blk in result.ImportedPlcBlocks)
                                {
                                    if (blk != null)
                                    {
                                        imported.Add(blk);
                                    }
                                }
                            }
                        }
                        catch (EngineeringNotSupportedException)
                        {
                            // mixed languages etc.; skip but continue batch
                        }
                        catch (Exception)
                        {
                            // skip problematic item, continue
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing blocks from documents");
            }

            return DescribeBlocks(imported);
        }

        public bool ImportFromDocuments(string softwarePath, string groupPath, string importPath, string fileNameWithoutExtension, string option)
        {
            _logger?.LogInformation($"Importing block from documents: {fileNameWithoutExtension} in {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportFromDocuments is only supported on TIA Portal V20 or newer");
                return false;
            }

            var importOption = ImportDocumentOption.Parse(option);

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    var dir = new DirectoryInfo(importPath);
                    if (!dir.Exists)
                    {
                        _logger?.LogWarning($"Import directory does not exist: {importPath}");
                        return false;
                    }

                    DocumentImportResult? result = null;
                    try
                    {
                        result = (group != null)
                            ? group.Blocks.ImportFromDocuments(dir, fileNameWithoutExtension, importOption)
                            : plcSoftware.BlockGroup.Blocks.ImportFromDocuments(dir, fileNameWithoutExtension, importOption);
                    }
                    catch (EngineeringNotSupportedException ex)
                    {
                        throw new PortalException(PortalErrorCode.ExportFailed, $"EngineeringNotSupportedException at file '{fileNameWithoutExtension}'. {ex.Message}", null, ex);
                    }

                    if (result != null && result.State == DocumentResultState.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing block from documents");
            }
            return false;
        }
    }
}
