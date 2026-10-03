using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Program blocks: reading them, describing them, exporting and importing them.
    ///
    /// Two rules from CLAUDE.md live in this file and nowhere else. A block is never assumed
    /// consistent -- TIA Portal refuses to export an inconsistent one and the native error does
    /// not say why -- and a path is always the full Group/Subgroup/Name, because a bare name is
    /// ambiguous.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Describes one block of a PLC program.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <param name="blockPath">Full path to the block, <c>Group/Subgroup/Name</c>.</param>
        /// <returns>The description, or null when there is no such block.</returns>
        /// <remarks>
        /// A description rather than the <c>PlcBlock</c> itself: see <see cref="BlockDescription"/>
        /// for why an engineering object must not leave this layer.
        /// </remarks>
        /// <exception cref="PortalException">The name is not a valid filter.</exception>
        public BlockDescription? GetBlock(string softwarePath, string blockPath)
        {
            var block = FindBlock(softwarePath, blockPath);

            return block == null ? null : BlockDescriber.Describe(block, GetBlockPath(block));
        }

        /// <summary>Describes the blocks of a PLC program, filtered by name.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <param name="regexName">The name filter, or empty for every block.</param>
        /// <returns>One description per matching block, in the order the program lists them.</returns>
        /// <exception cref="PortalException">
        /// The filter is not a valid expression, no project is open, or there is no PLC software at
        /// the path.
        /// </exception>
        public IReadOnlyList<BlockDescription> GetBlocks(string softwarePath, string regexName = "")
        {
            return DescribeBlocks(FindBlocks(softwarePath, regexName));
        }

        /// <summary>Describes a set of blocks, each with the path it was found at.</summary>
        /// <param name="blocks">The blocks to describe.</param>
        /// <returns>One description per block, in the order given.</returns>
        /// <remarks>
        /// Private because the blocks themselves are: this is the last thing that touches a
        /// <c>PlcBlock</c> before it goes out of scope for good.
        /// </remarks>
        private List<BlockDescription> DescribeBlocks(IEnumerable<PlcBlock> blocks)
        {
            var described = new List<BlockDescription>();
            foreach (var block in blocks)
            {
                described.Add(BlockDescriber.Describe(block, GetBlockPath(block)));
            }

            return described;
        }

        /// <summary>Describes the whole block tree of a PLC program.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <returns>The root group with its blocks and subgroups.</returns>
        /// <exception cref="PortalException">No project is open, or there is no PLC software at the path.</exception>
        public BlockGroupDescription GetBlockHierarchy(string softwarePath)
        {
            _logger?.LogInformation("Getting block root group...");

            return BlockDescriber.DescribeGroup(RequireSoftware(softwarePath).BlockGroup, string.Empty);
        }

        private PlcBlock? FindBlock(string softwarePath, string blockPath)
        {
            _logger?.LogInformation($"Getting block by path: {blockPath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup != null)
                {
                    var target = ProjectPath.Parse(blockPath);
                    var regexName = target.Name;

                    PlcBlock? block = null;

                    var group = GetPlcBlockGroupByPath(softwarePath, target.Parent);
                    if (group != null)
                    {
                        if (regexName.IndexOfAny(_regexChars) >= 0)
                        {
                            // Refused rather than returned as null: an invalid filter is not the
                            // same answer as "there is no such block", and reporting it as one
                            // sends the caller looking for a block instead of at their pattern.
                            var filter = NameFilter.Parse(regexName);

                            block = group.Blocks.FirstOrDefault(b => filter.Matches(b.Name)) as PlcBlock;
                        }
                        else
                        {
                            block = group.Blocks.FirstOrDefault(b => b.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase));
                        }

                        return block;
                    }
                }
            }

            return null;
        }

        /// <remarks>
        /// The path a caller writes, so that the one in a description can be handed straight back to
        /// GetBlock or ExportBlock. That means it stops below the root: a PLC program hangs off a
        /// <see cref="PlcBlockSystemGroup"/> called "Program blocks", and nothing accepts that name
        /// as the first segment of a path.
        ///
        /// <see cref="GetPlcBlockGroupPath"/> does include it, and is left alone: it lays out the
        /// directories of a preserve-path export, where the extra folder is harmless and changing it
        /// would move every file an existing snapshot already wrote.
        /// </remarks>
        private string GetBlockPath(PlcBlock block)
        {
            if (block == null)
            {
                return string.Empty;
            }

            if (block.Parent is PlcBlockGroup parentGroup)
            {
                var groupPath = GetUserBlockGroupPath(parentGroup);
                return string.IsNullOrEmpty(groupPath) ? block.Name : $"{groupPath}/{block.Name}";
            }

            return block.Name;
        }

        /// <remarks>
        /// Walks up from a group to the root, dropping the system group at the top. A user group
        /// never has a system group as an ancestor other than that root, so the loop ends there.
        /// </remarks>
        private string GetUserBlockGroupPath(PlcBlockGroup group)
        {
            var segments = new List<string>();

            PlcBlockGroup? current = group;
            while (current != null && !(current is PlcBlockSystemGroup))
            {
                segments.Insert(0, current.Name);
                current = current.Parent as PlcBlockGroup;
            }

            return string.Join("/", segments);
        }

        /// <remarks>
        /// Throws rather than answering an empty list. A software path that does not exist, no
        /// project open and an Openness failure all used to come back as "no blocks" — for an
        /// export, a shorter list that looks complete — and only the log knew why.
        /// </remarks>
        private List<PlcBlock> FindBlocks(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting blocks...");

            var list = new List<PlcBlock>();
            GetBlocksRecursive(RequireSoftware(softwarePath).BlockGroup, list, regexName);

            return list;
        }

        public BlockDescription? ExportBlock(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block by path: {blockPath}");

            try
            {
                // Resolved first so an unknown software path is named as such: the block lookup
                // answers null for it too, and "Block not found" sent callers looking for the block.
                RequireOfflineSoftware(softwarePath);

                var block = FindBlock(softwarePath, blockPath);

                if (block == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Block not found");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{block.Name}.xml");
                }

                // TIA Portal never exports inconsistent blocks
                if (!block.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Block is inconsistent; TIA Portal does not export inconsistent blocks.");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                block.Export(new FileInfo(exportPath), ExportOptions.None);

                return BlockDescriber.Describe(block, GetBlockPath(block));
            }
            catch (Exception ex)
            {
                //If the exception is already a PortalException, use it; otherwise, wrap it in a new PortalException
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
        }

        /// <summary>Imports a block from a SimaticML file, replacing a block of the same name.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="groupPath">Block group the block is placed in; empty for the root.</param>
        /// <param name="importPath">The SimaticML file.</param>
        /// <param name="backupDirectory">
        /// Where the program's blocks and types are exported before anything is written. Required:
        /// the import replaces a block of the same name.
        /// </param>
        /// <exception cref="PortalException">
        /// The block was not imported, or the backup was incomplete so nothing was; the message says why.
        /// </exception>
        /// <remarks>
        /// It used to answer false for every failure — no project, a group or a file that does not
        /// exist, TIA Portal rejecting the XML — and the tool could only say "failed".
        /// </remarks>
        public void ImportBlock(string softwarePath, string groupPath, string importPath, string backupDirectory)
        {
            _logger?.LogInformation($"Importing block from path: {importPath}");

            try
            {
                var software = RequireOfflineSoftware(softwarePath);
                var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Block group not found: '{groupPath}' in {softwarePath}");

                var file = RequireImportFile(importPath);

                ProgramBackup.Save(software, backupDirectory, _logger);
                var imported = group.Blocks.Import(file, ImportOptions.Override);

                if (imported == null || imported.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal imported no block from '{importPath}'");
                }
            }
            catch (Exception ex)
            {
                throw DecorateImportFailure(ex, "ImportBlock", softwarePath, groupPath, importPath);
            }
        }

        private static FileInfo RequireImportFile(string importPath)
        {
            if (string.IsNullOrWhiteSpace(importPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "importPath is required");
            }

            var file = new FileInfo(importPath);

            return file.Exists ? file : throw new PortalException(PortalErrorCode.NotFound, $"Import file does not exist: {importPath}");
        }

        private PortalException DecorateImportFailure(Exception ex, string operation, string softwarePath, string groupPath, string importPath)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["softwarePath"] = softwarePath;
            pex.Data["groupPath"] = groupPath;
            pex.Data["importPath"] = importPath;

            _logger?.LogError(pex, "{Operation} failed for {SoftwarePath} {GroupPath} <- {ImportPath}", operation, softwarePath, groupPath, importPath);
            return pex;
        }

        /// <summary>Exports every consistent block whose name matches as SimaticML.</summary>
        /// <param name="request">What to export and where to.</param>
        /// <param name="progress">
        /// Told how many of the selected items have been processed, after each one; null for none.
        /// </param>
        /// <param name="cancellationToken">Checked before each item; the files already written stay.</param>
        /// <returns>
        /// The blocks exported and the failures met. Inconsistent blocks are skipped, since
        /// SimaticML refuses them; the caller lists them from IsConsistent.
        /// </returns>
        /// <exception cref="PortalException">
        /// No project is open, there is no PLC software at the path, or the filter is not valid.
        /// </exception>
        /// <exception cref="OperationCanceledException">The export was cancelled.</exception>
        public ExportReport<BlockDescription> ExportBlocks(BulkExportRequest request, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Exporting blocks...");

            RequireOfflineSoftware(request.SoftwarePath);
            var selected = FindBlocks(request.SoftwarePath, request.RegexName);
            var exported = new List<PlcBlock>();
            var failures = new List<string>();

            for (var index = 0; index < selected.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryExportBlockXml(selected[index], request, failures))
                {
                    exported.Add(selected[index]);
                }

                progress?.Report(index + 1);
            }

            LogBulkExport("ExportBlocks", exported.Count, failures);
            return new ExportReport<BlockDescription>(DescribeBlocks(exported), failures);
        }

        private bool TryExportBlockXml(PlcBlock block, BulkExportRequest request, List<string> failures)
        {
            if (!block.IsConsistent)
            {
                return false;
            }

            var groupPath = request.PreservePath && block.Parent is PlcBlockGroup parentGroup ? GetPlcBlockGroupPath(parentGroup) : string.Empty;
            var path = XmlExportPath(request.ExportPath, groupPath, block.Name);

            return TryExportXml(block.Name, path, file => block.Export(file, ExportOptions.None), failures);
        }
    }
}
