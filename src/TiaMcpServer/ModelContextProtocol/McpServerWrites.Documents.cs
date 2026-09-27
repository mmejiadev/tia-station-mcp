using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
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
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                // Refused before a plan exists: a mistyped option is invalid input, not a change
                // that failed halfway through an import.
                TiaMcpServer.Siemens.ImportDocumentOption.Validate(importOption);

                // Pre-check .s7res for missing en-US tags
                var warnings = new JsonArray();
                try
                {
                    var missingIds = GetResMissingEnUsIds(importPath, fileNameWithoutExtension);
                    if (missingIds != null && missingIds.Count > 0)
                    {
                        Logger?.LogWarning($".s7res for '{fileNameWithoutExtension}' missing en-US tags for {missingIds.Count} items: {string.Join(", ", missingIds)}");
                        warnings.Add(new JsonObject
                        {
                            ["name"] = fileNameWithoutExtension,
                            ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to evaluate .s7res warnings");
                }

                var request = new Governance.ChangeRequest(
                    "ImportFromDocuments",
                    ChangeTarget.Program(softwarePath, groupPath),
                    fileNameWithoutExtension);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        if (!Portal.ImportFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, importOption))
                        {
                            throw new McpException($"Failed importing '{fileNameWithoutExtension}' from '{importPath}'", McpErrorCode.InternalError);
                        }

                        return new ResponseImportFromDocuments
                        {
                            Message = $"Imported '{fileNameWithoutExtension}' from '{importPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["warnings"] = warnings
                            }
                        };
                    },
                    () => new ResponseImportFromDocuments());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed importing '{fileNameWithoutExtension}' from '{importPath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing from documents: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportBlocksFromDocuments"), Description("Import program blocks from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+)")]
        public static async Task<ResponseImportBlocksFromDocuments> ImportBlocksFromDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the blocks should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("regexName: name or regular expression to select block files (empty for all)")] string regexName = "",
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            var startTime = DateTime.Now;

            // No context means no progress token, which is the same condition as a caller that
            // did not ask for progress: this tool reports none and does the work. It is not
            // defensive padding — RequestContext cannot be constructed without a live server, so
            // a caller with no server to notify has no context to pass either.
            var progressToken = context?.Params?.ProgressToken;

            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportBlocksFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                // Determine total by scanning .s7dcl files matching regex
                int total = 0;
                var scanWarnings = new JsonArray();

                // The pre-scan only counts files and warns about missing en-US resources, so a
                // failure here must not stop the import - but it is logged rather than swallowed.
                // An empty catch is forbidden outright by CLAUDE.md, and this file held three of
                // them until the audit of 2026-09-02: a warning that never appeared looked exactly
                // like a document with nothing wrong with it.
                try
                {
                    if (Directory.Exists(importPath))
                    {
                        var filter = Siemens.NameFilter.Parse(regexName);
                        var files = Directory.GetFiles(importPath, "*.s7dcl", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(f);
                            if (!filter.Matches(name))
                                continue;
                            total++;

                            try
                            {
                                var missingIds = GetResMissingEnUsIds(importPath, name);
                                if (missingIds != null && missingIds.Count > 0)
                                {
                                    scanWarnings.Add(new JsonObject
                                    {
                                        ["name"] = name,
                                        ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                                    });
                                }
                            }
                            catch (Exception scanFailure)
                            {
                                Logger?.LogWarning(scanFailure, "Could not read the en-US resources of '{Name}' in '{ImportPath}'", name, importPath);
                            }
                        }
                    }
                }
                catch (Exception scanFailure)
                {
                    Logger?.LogWarning(scanFailure, "The pre-scan of '{ImportPath}' failed; the import continues without its warnings", importPath);
                }

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = total,
                        Message = total > 0 ? $"Starting import of {total} blocks from documents..." : "Scanning import directory...",
                        progressToken
                    });
                }

                // Refused before a plan exists: a mistyped option is invalid input, not a change
                // that failed halfway through an import.
                TiaMcpServer.Siemens.ImportDocumentOption.Validate(importOption);

                var request = new Governance.ChangeRequest(
                    "ImportBlocksFromDocuments",
                    ChangeTarget.Program(softwarePath, groupPath),
                    string.IsNullOrWhiteSpace(regexName) ? importPath : regexName);

                // The guard decides on the calling thread and the import runs on a worker, so a
                // refusal costs nothing and the import that does run still keeps this method
                // responsive enough to report progress.
                var response = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var imported = Portal.ImportBlocksFromDocuments(softwarePath, groupPath, importPath, regexName, importOption);
                        var items = DescribeBlocks(imported);

                        return new ResponseImportBlocksFromDocuments
                        {
                            Message = $"Document import completed: {items.Count} blocks imported from '{importPath}'",
                            Items = items,
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["totalBlocks"] = total,
                                ["importedBlocks"] = items.Count,
                                ["duration"] = (DateTime.Now - startTime).TotalSeconds,
                                ["warnings"] = scanWarnings
                            }
                        };
                    },
                    () => new ResponseImportBlocksFromDocuments())));

                var processed = response.Items?.Count() ?? 0;

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = processed,
                        Total = total,
                        Message = response.Message,
                        progressToken
                    });
                }

                Logger?.LogInformation(
                    "Document import finished: {Processed} block(s) in {Duration:F2} s",
                    processed,
                    (DateTime.Now - startTime).TotalSeconds);

                return response;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document import failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch (Exception notifyFailure)
                    {
                        // Already handling a failure: this one must not replace it, but a progress
                        // channel that has quietly died is worth knowing about.
                        Logger?.LogWarning(notifyFailure, "Could not send the failure notification for '{ImportPath}'", importPath);
                    }
                }

                if (ex is TiaMcpServer.Siemens.PortalException pex)
                {
                    // A refused import option is invalid input, not a broken environment, and
                    // telling the caller to retry it would be wrong.
                    throw ToMcpException(pex, $"Failed importing documents from '{importPath}'");
                }

                Logger?.LogError(ex, $"Failed importing documents from '{importPath}'");
                throw new McpException($"Unexpected error importing documents from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
    }
}
