using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Exporting blocks as SIMATIC SD documents, and the .s7res check that goes with importing.
    ///
    /// GetResMissingEnUsIds is here rather than with the import that calls it, because what it
    /// knows is the document format: a LAD block imported without en-US tags in its .s7res fails
    /// with an error naming neither the file nor the language.
    /// </remarks>
    public static partial class McpServer
    {
        private const int FirstVersionWithDocuments = 20;

        [McpServerTool(Name = "ExportAsDocuments"), Description("Export as documents (.s7dcl/.s7res) from a block in the plc software to path")]
        public static ResponseExportAsDocuments ExportAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                RequireDocumentSupport("ExportAsDocuments");
                if (Portal.ExportAsDocuments(softwarePath, blockPath, exportPath, preservePath))
                {
                    return new ResponseExportAsDocuments
                    {
                        Message = $"Documents exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting documents from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocksAsDocuments"), Description("Export as documents (.s7dcl/.s7res) from blocks in the plc software to path. Blocks that do not compile are exported too — this is the one export that can read a broken block — and are listed in Inconsistent. Blocks that could not be exported are named in Failed, with the reason.")]
        public static async Task<ResponseExportBlocksAsDocuments> ExportBlocksAsDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var call = new BulkCall(softwarePath, exportPath, regexName);
            var progress = ProgressReporter.For(server, context, Logger);

            try
            {
                RequireDocumentSupport("ExportBlocksAsDocuments");

                Logger?.LogInformation($"Starting export of blocks as documents from '{softwarePath}' to '{exportPath}'");

                var allBlocks = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.GetBlocks(softwarePath, regexName)));

                if (allBlocks.Count == 0)
                {
                    await progress.ReportAsync(0, 0, "No blocks found to export as documents");
                    return NoDocumentsExported(call);
                }

                return await ExportDocuments(call, preservePath, allBlocks.Count, progress);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                await progress.ReportAsync(0, 0, $"Document export failed: {ex.Message}");
                throw ToolFailure(ex, $"exporting documents to '{exportPath}'");
            }
        }

        private static async Task<ResponseExportBlocksAsDocuments> ExportDocuments(BulkCall call, bool preservePath, int totalBlocks, ProgressReporter progress)
        {
            await progress.ReportAsync(0, totalBlocks, $"Starting export of {totalBlocks} blocks as documents...");

            var report = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.ExportBlocksAsDocuments(call.SoftwarePath, call.Directory, call.RegexName, preservePath)));

            var response = DocumentsExported(call, totalBlocks, report);
            await progress.ReportAsync(report.Exported.Count, totalBlocks, $"Document export completed: {report.Exported.Count} blocks exported");
            return response;
        }

        private static ResponseExportBlocksAsDocuments NoDocumentsExported(BulkCall call)
        {
            return new ResponseExportBlocksAsDocuments
            {
                Message = $"No blocks found with regex '{call.RegexName}' in '{call.SoftwarePath}'",
                Items = new List<ResponseBlockInfo>(),
                Inconsistent = new List<ResponseBlockInfo>(),
                Failed = new List<string>(),
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
        /// Unlike the SimaticML exports, inconsistent blocks are read from what was exported: SD
        /// export writes them, so they are among the items rather than missing from them.
        /// </remarks>
        private static ResponseExportBlocksAsDocuments DocumentsExported(BulkCall call, int totalBlocks, DocumentExportReport report)
        {
            var items = DescribeBlocks(report.Exported);
            var inconsistent = items.Where(block => block.IsConsistent == false).ToList();
            Logger?.LogInformation($"Document export completed: {items.Count} blocks exported in {call.ElapsedSeconds:F2} seconds");

            return new ResponseExportBlocksAsDocuments
            {
                Message = DocumentExportMessage(call, items.Count, inconsistent.Count, report.Failures.Count),
                Items = items,
                Inconsistent = inconsistent,
                Failed = report.Failures,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = report.Failures.Count == 0,
                    ["totalBlocks"] = totalBlocks,
                    ["exportedBlocks"] = items.Count,
                    ["inconsistentBlocks"] = inconsistent.Count,
                    ["failedBlocks"] = report.Failures.Count,
                    ["duration"] = call.ElapsedSeconds
                }
            };
        }

        private static string DocumentExportMessage(BulkCall call, int exportedCount, int inconsistentCount, int failureCount)
        {
            var message = $"Document export completed: {exportedCount} blocks with regex '{call.RegexName}' exported from '{call.SoftwarePath}' to '{call.Directory}'";

            if (inconsistentCount > 0)
            {
                message += $"; {inconsistentCount} of them do not compile and were exported as they stand (see Inconsistent)";
            }

            if (failureCount > 0)
            {
                message += $"; {failureCount} problems prevented a full export (see Failed)";
            }

            return message;
        }

        /// <remarks>
        /// SIMATIC SD documents arrived with TIA Portal V20; older versions have no API for them, and
        /// asking one fails with an error that does not say so.
        /// </remarks>
        private static void RequireDocumentSupport(string toolName)
        {
            if (Engineering.TiaMajorVersion < FirstVersionWithDocuments)
            {
                throw new McpException($"{toolName} requires TIA Portal V{FirstVersionWithDocuments} or newer", McpErrorCode.InvalidParams);
            }
        }

        private static List<string> GetResMissingEnUsIds(string directory, string baseName)
        {
            var resPath = Path.Combine(directory, baseName + ".s7res");
            var missing = new List<string>();
            if (!File.Exists(resPath))
            {
                return missing;
            }
            var xdoc = XDocument.Load(resPath);
            XNamespace ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
            foreach (var comment in xdoc.Descendants(ns + "Comment"))
            {
                var hasEnUs = comment.Elements(ns + "MultiLanguageText")
                                     .Any(e => string.Equals((string?)e.Attribute("Lang"), "en-US", StringComparison.OrdinalIgnoreCase));
                if (!hasEnUs)
                {
                    var id = (string?)comment.Attribute("Id") ?? "";
                    missing.Add(id);
                }
            }
            return missing;
        }
    }
}
