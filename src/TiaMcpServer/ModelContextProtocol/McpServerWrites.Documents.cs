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
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Importing blocks from SIMATIC SD documents.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ImportFromDocuments"), Description("Import program block from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+)")]
        public static ResponseImportFromDocuments ImportFromDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the block should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("fileNameWithoutExtension: name of the block file without extension") ] string fileNameWithoutExtension,
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                RequireDocumentSupport("ImportFromDocuments");

                // Refused before a plan exists: a mistyped option is invalid input, not a change
                // that failed halfway through an import.
                TiaMcpServer.Siemens.ImportDocumentOption.Validate(importOption);

                var warnings = MissingEnUsWarnings(importPath, new[] { fileNameWithoutExtension });

                var request = new Governance.ChangeRequest(
                    "ImportFromDocuments",
                    ChangeTarget.Program(softwarePath, groupPath),
                    fileNameWithoutExtension);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => BlockImported(() => Portal.ImportFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, importOption), fileNameWithoutExtension, importPath, warnings),
                    () => new ResponseImportFromDocuments());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"importing '{fileNameWithoutExtension}' from '{importPath}'");
            }
        }

        /// <remarks>
        /// The import throws when the block is not imported, with the reason; it used to answer false,
        /// and the tool could only say "failed" without saying why.
        /// </remarks>
        private static ResponseImportFromDocuments BlockImported(Action import, string documentName, string importPath, JsonArray warnings)
        {
            import();

            return new ResponseImportFromDocuments
            {
                Message = $"Imported '{documentName}' from '{importPath}'",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["warnings"] = warnings
                }
            };
        }

        [McpServerTool(Name = "ImportBlocksFromDocuments"), Description("Import program blocks from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+). Documents that could not be imported are named in Failed, with the reason; a groupPath that does not exist is refused rather than read as the root.")]
        public static async Task<ResponseImportBlocksFromDocuments> ImportBlocksFromDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the blocks should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("regexName: name or regular expression to select block files (empty for all)")] string regexName = "",
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            var call = new BulkCall(softwarePath, importPath, regexName);
            var progress = ProgressReporter.For(server, context, Logger);

            try
            {
                RequireDocumentSupport("ImportBlocksFromDocuments");

                var documents = FindDocuments(importPath, regexName);
                await progress.ReportAsync(0, documents.Count, documents.Count > 0 ? $"Starting import of {documents.Count} blocks from documents..." : "Scanning import directory...");

                var response = await ImportDocumentsGuarded(call, groupPath, importOption, documents);

                var processed = response.Items?.Count() ?? 0;
                await progress.ReportAsync(processed, documents.Count, response.Message ?? string.Empty);
                Logger?.LogInformation("Document import finished: {Processed} block(s) in {Duration:F2} s", processed, call.ElapsedSeconds);
                return response;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                await progress.ReportAsync(0, 0, $"Document import failed: {ex.Message}");
                throw ToolFailure(ex, $"importing documents from '{importPath}'");
            }
        }

        /// <remarks>
        /// The guard decides on the calling thread and the import runs on a worker, so a refusal
        /// costs nothing and the import that does run still keeps the tool responsive enough to
        /// report progress.
        /// </remarks>
        private static Task<ResponseImportBlocksFromDocuments> ImportDocumentsGuarded(BulkCall call, string groupPath, string importOption, IReadOnlyList<string> documents)
        {
            // Refused before a plan exists: a mistyped option is invalid input, not a change
            // that failed halfway through an import.
            TiaMcpServer.Siemens.ImportDocumentOption.Validate(importOption);

            var warnings = MissingEnUsWarnings(call.Directory, documents);
            var request = new Governance.ChangeRequest(
                "ImportBlocksFromDocuments",
                ChangeTarget.Program(call.SoftwarePath, groupPath),
                string.IsNullOrWhiteSpace(call.RegexName) ? call.Directory : call.RegexName);

            return Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => GuardedTool.Run(
                GuardedWrites,
                request,
                () => BlocksImported(call, documents.Count, warnings, Portal.ImportBlocksFromDocuments(call.SoftwarePath, groupPath, call.Directory, call.RegexName, importOption)),
                () => new ResponseImportBlocksFromDocuments())));
        }

        private static ResponseImportBlocksFromDocuments BlocksImported(BulkCall call, int documentCount, JsonArray warnings, DocumentImportReport report)
        {
            var items = DescribeBlocks(report.Imported);

            return new ResponseImportBlocksFromDocuments
            {
                Message = DocumentImportMessage(call, items.Count, report.Failures.Count),
                Items = items,
                Failed = report.Failures,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = report.Failures.Count == 0,
                    ["totalBlocks"] = documentCount,
                    ["importedBlocks"] = items.Count,
                    ["failedBlocks"] = report.Failures.Count,
                    ["duration"] = call.ElapsedSeconds,
                    ["warnings"] = warnings
                }
            };
        }

        private static string DocumentImportMessage(BulkCall call, int importedCount, int failureCount)
        {
            var message = $"Document import completed: {importedCount} blocks imported from '{call.Directory}'";

            if (failureCount > 0)
            {
                message += $"; {failureCount} documents were not imported (see Failed)";
            }

            return message;
        }

        /// <remarks>
        /// The pre-scan only counts documents and warns about missing en-US resources, so a failure
        /// here must not stop the import — but it is logged rather than swallowed. An empty catch is
        /// forbidden outright by CLAUDE.md, and this tool held three of them until the audit of
        /// 2026-09-02: a warning that never appeared looked exactly like a document with nothing
        /// wrong with it.
        /// </remarks>
        private static IReadOnlyList<string> FindDocuments(string importPath, string regexName)
        {
            try
            {
                if (!Directory.Exists(importPath))
                {
                    return Array.Empty<string>();
                }

                var filter = Siemens.NameFilter.Parse(regexName);
                return Directory.GetFiles(importPath, "*.s7dcl", SearchOption.TopDirectoryOnly)
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .Where(name => filter.Matches(name))
                    .ToList();
            }
            catch (Exception scanFailure)
            {
                Logger?.LogWarning(scanFailure, "The pre-scan of '{ImportPath}' failed; the import continues without its warnings", importPath);
                return Array.Empty<string>();
            }
        }

        private static JsonArray MissingEnUsWarnings(string importPath, IEnumerable<string> documentNames)
        {
            var warnings = new JsonArray();

            foreach (var name in documentNames)
            {
                var warning = MissingEnUsWarning(importPath, name);

                if (warning != null)
                {
                    warnings.Add(warning);
                }
            }

            return warnings;
        }

        /// <remarks>
        /// A LAD block imported without en-US tags in its .s7res fails with an error naming neither
        /// the file nor the language, so the gap is named before the import is tried. Reading the
        /// resources is advisory: a failure is logged and the import goes ahead.
        /// </remarks>
        private static JsonObject? MissingEnUsWarning(string importPath, string documentName)
        {
            try
            {
                var missingIds = GetResMissingEnUsIds(importPath, documentName);

                if (missingIds.Count == 0)
                {
                    return null;
                }

                Logger?.LogWarning(".s7res for '{Name}' is missing en-US tags for {Count} items: {Ids}", documentName, missingIds.Count, string.Join(", ", missingIds));
                return new JsonObject
                {
                    ["name"] = documentName,
                    ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                };
            }
            catch (Exception scanFailure)
            {
                Logger?.LogWarning(scanFailure, "Could not read the en-US resources of '{Name}' in '{ImportPath}'", documentName, importPath);
                return null;
            }
        }
    }
}
