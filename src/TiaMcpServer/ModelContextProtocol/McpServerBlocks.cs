using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading and exporting program blocks.
    ///
    /// Everything here takes a BlockDescription from the portal layer and reshapes it into the
    /// response contract. It never touches a PlcBlock -- see CLAUDE.md's dependency rule, and
    /// audit finding F2, which was about this file holding eight copies of that translation.
    /// </remarks>
    public static partial class McpServer
    {
        /// <summary>How many full paths a "block not found" answer offers at most.</summary>
        private const int MaxBlockSuggestions = 10;

        [McpServerTool(Name = "GetBlockInfo"), Description("Get a block info, which is located in the plc software")]
        public static ResponseBlockInfo GetBlockInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var block = Portal.GetBlock(softwarePath, blockPath);
                if (block != null)
                {
                    var info = Describe(block);
                    info.Message = $"Block info retrieved from '{blockPath}' in '{softwarePath}'";
                    info.Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    };

                    return info;
                }
                else
                {
                    throw new McpException($"Block not found at '{blockPath}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving block info from '{blockPath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlocks"), Description("Get a list of blocks, which are located in plc software")]
        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetBlocks(softwarePath, regexName);

                return new ResponseBlocks
                {
                    Message = $"Blocks with regex '{regexName}' retrieved from '{softwarePath}'",
                    Items = DescribeBlocks(list),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"retrieving blocks with regex '{regexName}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "GetBlocksWithHierarchy"), Description("Get a list of all blocks with their group hierarchy from the plc software.")]
        public static ResponseBlocksWithHierarchy GetBlocksWithHierarchy(
        [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                return new ResponseBlocksWithHierarchy
                {
                    Message = $"Block hierarchy retrieved from '{softwarePath}'",
                    Root = Portal.GetBlockHierarchy(softwarePath),
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"retrieving block hierarchy for '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "ExportBlock"), Description("Export a block from plc software to file")]
        public static ResponseExportBlock ExportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath,
            [Description("exportPath: defines the path where to export the block")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                Portal.ExportBlock(softwarePath, blockPath, exportPath, preservePath);

                return new ResponseExportBlock
                {
                    Message = $"Block exported from '{blockPath}' to '{exportPath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex) when (pex.Code == TiaMcpServer.Siemens.PortalErrorCode.NotFound)
            {
                throw new McpException($"{pex.Message}.{SuggestBlockPaths(softwarePath, blockPath)}", McpErrorCode.InvalidParams);
            }
            catch (TiaMcpServer.Siemens.PortalException pex) when (pex.Code == TiaMcpServer.Siemens.PortalErrorCode.ExportFailed)
            {
                // The portal wraps what TIA Portal threw as "Export failed"; the reason is inside.
                Logger?.LogError(pex, "MCP ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw new McpException($"Failed to export block. Reason: {pex.InnerException?.Message ?? pex.Message}", McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"exporting block from '{blockPath}' to '{exportPath}'");
            }
        }

        /// <remarks>
        /// A bare name is the usual reason a block is not found, since paths are required. The
        /// suggestion is advice on top of the answer, never the answer: if looking it up fails, the
        /// caller still gets "not found" and the failure goes to the log. It used to go nowhere, in
        /// an empty catch.
        /// </remarks>
        private static string SuggestBlockPaths(string softwarePath, string blockPath)
        {
            if (string.IsNullOrEmpty(blockPath) || blockPath.Contains('/'))
            {
                return string.Empty;
            }

            try
            {
                var candidates = FindBlockPathsNamed(softwarePath, Regex.Escape(blockPath));

                return candidates.Count == 0 ? string.Empty : $" Did you mean: {string.Join(", ", candidates)}?";
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Could not look up suggestions for block '{BlockPath}' in '{SoftwarePath}'", blockPath, softwarePath);
                return string.Empty;
            }
        }

        private static List<string> FindBlockPathsNamed(string softwarePath, string escapedName)
        {
            var blocks = Portal.GetBlocks(softwarePath, $"^{escapedName}$");
            if (blocks.Count == 0)
            {
                blocks = Portal.GetBlocks(softwarePath, escapedName);
            }

            return blocks
                .Take(MaxBlockSuggestions)
                .Select(block => block.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        [McpServerTool(Name = "ExportBlocks"), Description("Export all blocks from the plc software to path")]
        public static async Task<ResponseExportBlocks> ExportBlocks(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the blocks")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var call = new BulkCall(softwarePath, exportPath, regexName);
            var progress = ProgressReporter.For(server, context, Logger);

            try
            {
                Logger?.LogInformation($"Starting export of blocks from '{softwarePath}' to '{exportPath}'");

                var allBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.GetBlocks(softwarePath, regexName)));

                if (allBlocks.Count == 0)
                {
                    await progress.ReportAsync(0, 0, "No blocks found to export");
                    return NoBlocksExported(call);
                }

                await progress.ReportAsync(0, allBlocks.Count, $"Starting export of {allBlocks.Count} blocks...");

                var exportedBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.ExportBlocks(softwarePath, exportPath, regexName, preservePath)));

                var response = BlocksExported(call, allBlocks, exportedBlocks);
                await progress.ReportAsync(exportedBlocks.Count, allBlocks.Count, $"Export completed: {exportedBlocks.Count} blocks exported successfully");
                return response;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                await progress.ReportAsync(0, 0, $"Export failed: {ex.Message}");
                throw ToolFailure(ex, $"exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}");
            }
        }

        private static ResponseExportBlocks NoBlocksExported(BulkCall call)
        {
            return new ResponseExportBlocks
            {
                Message = $"No blocks found with regex '{call.RegexName}' in '{call.SoftwarePath}'",
                Items = new List<ResponseBlockInfo>(),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["totalBlocks"] = 0,
                    ["exportedBlocks"] = 0,
                    ["duration"] = call.ElapsedSeconds
                }
            };
        }

        /// <remarks>
        /// Inconsistent blocks are reported from the list read before the export, not from what it
        /// returned: SimaticML export skips them, so they are exactly the ones missing from it.
        /// </remarks>
        private static ResponseExportBlocks BlocksExported(BulkCall call, IReadOnlyList<BlockDescription> allBlocks, IReadOnlyList<BlockDescription> exportedBlocks)
        {
            var items = DescribeBlocks(exportedBlocks);
            var inconsistent = DescribeBlocks(allBlocks.Where(block => !block.IsConsistent));
            Logger?.LogInformation($"Export completed: {items.Count} blocks exported in {call.ElapsedSeconds:F2} seconds");

            return new ResponseExportBlocks
            {
                Message = $"Export completed: {items.Count} blocks with regex '{call.RegexName}' exported from '{call.SoftwarePath}' to '{call.Directory}'",
                Items = items,
                Inconsistent = inconsistent,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["totalBlocks"] = allBlocks.Count,
                    ["exportedBlocks"] = items.Count,
                    ["inconsistentBlocks"] = inconsistent.Count,
                    ["duration"] = call.ElapsedSeconds
                }
            };
        }

        /// <remarks>
        /// The portal layer has already read everything; this only reshapes it into the response
        /// contract. It used to read the blocks itself, from live <c>PlcBlock</c> objects, at eight
        /// sites with eight copies of the same dozen assignments.
        /// </remarks>
        private static List<ResponseBlockInfo> DescribeBlocks(IEnumerable<BlockDescription>? blocks)
        {
            var described = new List<ResponseBlockInfo>();

            if (blocks == null)
            {
                return described;
            }

            foreach (var block in blocks)
            {
                described.Add(Describe(block));
            }

            return described;
        }

        private static ResponseBlockInfo Describe(BlockDescription block)
        {
            return new ResponseBlockInfo
            {
                Name = block.Name,
                Path = block.Path,
                TypeName = block.TypeName,
                Namespace = block.Namespace,
                ProgrammingLanguage = block.ProgrammingLanguage,
                MemoryLayout = block.MemoryLayout,
                IsConsistent = block.IsConsistent,
                HeaderName = block.HeaderName,
                ModifiedDate = block.ModifiedDate,
                IsKnowHowProtected = block.IsKnowHowProtected,
                Attributes = block.Attributes,
                Description = block.Description
            };
        }
    }
}
