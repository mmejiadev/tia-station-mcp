using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// <exception cref="PortalException">The filter is not a valid expression.</exception>
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
        /// <returns>The root group with its blocks and subgroups, or null when it cannot be read.</returns>
        public BlockGroupDescription? GetBlockHierarchy(string softwarePath)
        {
            var root = FindBlockRootGroup(softwarePath);

            return root == null ? null : BlockDescriber.DescribeGroup(root, string.Empty);
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
                    var path = blockPath.Contains("/") ? blockPath.Substring(0, blockPath.LastIndexOf("/")) : string.Empty;
                    var regexName = blockPath.Contains("/") ? blockPath.Substring(blockPath.LastIndexOf("/") + 1) : blockPath;

                    PlcBlock? block = null;

                    var group = GetPlcBlockGroupByPath(softwarePath, path);
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

        private List<PlcBlock> FindBlocks(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting blocks...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.BlockGroup;

                    if (group != null)
                    {
                        GetBlocksRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting blocks from {SoftwarePath} with regex {RegexName}", softwarePath, regexName);
            }

            return list;
        }

        private PlcBlockSystemGroup? FindBlockRootGroup(string softwarePath)
        {
            _logger?.LogInformation("Getting block root group...");

            if (IsProjectNull())
            {
                return null;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    return plcSoftware.BlockGroup;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting block root group");
            }

            return null;
        }

        public BlockDescription? ExportBlock(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block by path: {blockPath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

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

        public bool ImportBlock(string softwarePath, string groupPath, string importPath)
        {
            _logger?.LogInformation($"Importing block from path: {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup != null)
                {

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    if (group == null)
                    {
                        return false;
                    }

                    try
                    {
                        // Correct the argument type by using FileInfo instead of FileStream  
                        var fileInfo = new FileInfo(importPath);
                        if (fileInfo.Exists)
                        {
                            var list = group.Blocks.Import(fileInfo, ImportOptions.Override);
                            if (list != null && list.Count > 0)
                            {
                                return true;
                            }
                        }

                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public IReadOnlyList<BlockDescription>? ExportBlocks(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting blocks...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();
            
            PlcBlock[] list;

            try
            {
                list = FindBlocks(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve block list for {SoftwarePath}", softwarePath);
                return DescribeBlocks(exportList);
            }

            for (int k = 0; k < list.Count(); k++)
            {
                var block = list[k];

                _logger?.LogDebug($"- Exporting block {k}/{list.Count()} : {block.Name}");

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{block.Name}.xml");
                }

                try
                {
                    if (!block.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent block {Name}", block.Name);

                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try { File.Delete(path); }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{block.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);

                            continue;
                        }
                    }

                    try
                    {
                        block.Export(new FileInfo(path), ExportOptions.None);
                    }
                    catch (LicenseNotFoundException licEx)
                    {
                        failures.Add($"{block.Name}: license not found ({licEx.Message})");
                        _logger?.LogError(licEx, "License issue exporting {Block}", block.Name);

                        continue;
                    }
                    catch (EngineeringTargetInvocationException engEx)
                    {
                        failures.Add($"{block.Name}: target invocation failed ({engEx.Message})");
                        _logger?.LogError(engEx, "TargetInvocationException exporting {Block}", block.Name);

                        continue;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for {Block}", block.Name);

                        continue;
                    }

                    exportList.Add(block);
                }
                catch (Exception ex)
                {
                    // Catch only truly unexpected wrapper-level errors
                    failures.Add($"{block.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at block {Block}", block.Name);
                    // continue with next block
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportBlocks completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
                // Optionally: _logger?.LogDebug("All failures: {Failures}", string.Join("; ", failures));
            }
            else
            {
                _logger?.LogInformation($"ExportBlocks completed successfully. Exported {exportList.Count} blocks.");
            }

            return DescribeBlocks(exportList);
        }
    }
}
