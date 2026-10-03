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
    /// Importing blocks from SIMATIC SD documents, apart from exporting them because the two fail
    /// differently: an export that fails leaves a file missing, an import that fails leaves the
    /// project missing a block the caller will go on to compile and download.
    ///
    /// Both methods used to catch everything, log it and answer with an empty list or false. A
    /// missing project, a group that does not exist, an invalid name filter and TIA Portal failing
    /// all looked like "nothing to import".
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Imports every SIMATIC SD document (.s7dcl, with its .s7res when present) in a directory
        /// whose name matches.
        /// </summary>
        /// <remarks>
        /// A document that fails to import is left out and named in the report's failures; the others
        /// are still imported. Anything that stops the whole import — no project, no such group, an
        /// invalid filter, an incomplete backup — is thrown instead, so it cannot be mistaken for an
        /// empty directory.
        /// </remarks>
        /// <param name="request">Where to import from and to, and where to save the previous state.</param>
        /// <param name="regexName">Name or regular expression selecting the documents; empty for all.</param>
        /// <param name="option">An ImportDocumentOptions value, or empty for Override.</param>
        /// <returns>The blocks imported and the documents that were not.</returns>
        /// <exception cref="PortalException">The import could not start, or failed as a whole.</exception>
        public DocumentImportReport ImportBlocksFromDocuments(DocumentImportRequest request, string regexName, string option)
        {
            _logger?.LogInformation($"Importing blocks from documents in {request.ImportPath} with regex '{regexName}'");

            try
            {
                var directory = RequireImportDirectory(request.ImportPath);
                var documentNames = DocumentNames(directory, NameFilter.Parse(regexName));
                var importOption = ImportDocumentOption.Parse(option);
                var destination = PrepareDocumentDestination(request);

                return ImportDocuments(documentNames, name => destination.ImportFromDocuments(directory, name, importOption));
            }
            catch (Exception ex)
            {
                throw DecorateDocumentImportFailure(ex, "ImportBlocksFromDocuments", request, regexName);
            }
        }

        /// <summary>Imports one block from its SIMATIC SD documents (.s7dcl, with its .s7res when present).</summary>
        /// <param name="request">Where to import from and to, and where to save the previous state.</param>
        /// <param name="fileNameWithoutExtension">The documents' file name, without extension.</param>
        /// <param name="option">An ImportDocumentOptions value, or empty for Override.</param>
        /// <exception cref="PortalException">
        /// The block was not imported, or the backup was incomplete so nothing was; the message says why.
        /// </exception>
        public void ImportFromDocuments(DocumentImportRequest request, string fileNameWithoutExtension, string option)
        {
            _logger?.LogInformation($"Importing block from documents: {fileNameWithoutExtension} in {request.ImportPath}");

            try
            {
                var directory = RequireImportDirectory(request.ImportPath);
                var importOption = ImportDocumentOption.Parse(option);
                var destination = PrepareDocumentDestination(request);
                var failure = DocumentImportFailure(destination.ImportFromDocuments(directory, fileNameWithoutExtension, importOption));

                if (failure != null)
                {
                    throw new PortalException(PortalErrorCode.WriteFailed, $"Importing '{fileNameWithoutExtension}' failed: {failure}");
                }
            }
            catch (Exception ex)
            {
                throw DecorateDocumentImportFailure(ex, "ImportFromDocuments", request, fileNameWithoutExtension);
            }
        }

        private PortalException DecorateDocumentImportFailure(Exception ex, string operation, DocumentImportRequest request, string selection)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Import from documents failed: {ex.Message}", null, ex);

            pex.Data["softwarePath"] = request.SoftwarePath;
            pex.Data["groupPath"] = request.GroupPath;
            pex.Data["importPath"] = request.ImportPath;
            pex.Data["backupDirectory"] = request.BackupDirectory;
            pex.Data["selection"] = selection;

            _logger?.LogError(pex, "{Operation} failed for {SoftwarePath} {GroupPath} <- {ImportPath} {Selection}", operation, request.SoftwarePath, request.GroupPath, request.ImportPath, selection);
            return pex;
        }

        /// <remarks>
        /// A group that does not exist is refused rather than read as the root. The lookup answers
        /// null for both "no such group" and "no group asked for", and the import used to take the
        /// null to mean the root: a mistyped group put the blocks at the top of the program, and the
        /// tool reported them imported.
        ///
        /// The backup comes last, once everything that can refuse the import has been checked, and
        /// before anything is written.
        /// </remarks>
        private PlcBlockComposition PrepareDocumentDestination(DocumentImportRequest request)
        {
            RequireDocumentSupport();

            var software = RequireOfflineSoftware(request.SoftwarePath);
            var group = string.IsNullOrWhiteSpace(request.GroupPath)
                ? software.BlockGroup
                : GetPlcBlockGroupByPath(request.SoftwarePath, request.GroupPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Block group not found: '{request.GroupPath}' in {request.SoftwarePath}. Create it first, or leave groupPath empty to import at the root.");

            ProgramBackup.Save(software, request.BackupDirectory, _logger);
            return group.Blocks;
        }

        private static DirectoryInfo RequireImportDirectory(string importPath)
        {
            if (string.IsNullOrWhiteSpace(importPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "importPath is required");
            }

            var directory = new DirectoryInfo(importPath);

            if (!directory.Exists)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Import directory does not exist: {importPath}");
            }

            return directory;
        }

        /// <remarks>
        /// The .s7dcl is the document; the .s7res beside it is optional and TIA Portal reads it on its own.
        /// </remarks>
        private static List<string> DocumentNames(DirectoryInfo directory, NameFilter filter)
        {
            return directory.GetFiles("*.s7dcl", SearchOption.TopDirectoryOnly)
                .Select(file => Path.GetFileNameWithoutExtension(file.Name))
                .Where(filter.Matches)
                .ToList();
        }

        private DocumentImportReport ImportDocuments(IReadOnlyList<string> documentNames, Func<string, DocumentImportResultForBlocks> import)
        {
            var imported = new List<PlcBlock>();
            var failures = new List<string>();

            foreach (var documentName in documentNames)
            {
                imported.AddRange(TryImportDocument(import, documentName, failures));
            }

            LogDocumentImport(imported.Count, failures);
            return new DocumentImportReport(DescribeBlocks(imported), failures);
        }

        /// <remarks>
        /// One document failing must not stop the others, so each failure is recorded rather than
        /// thrown, the same rule as the bulk document export; the log keeps the exception. The blocks
        /// TIA Portal names are returned even when the document is also a failure: after a partial
        /// success they are in the project, and leaving them out would hide a change that happened.
        /// </remarks>
        private IReadOnlyList<PlcBlock> TryImportDocument(Func<string, DocumentImportResultForBlocks> import, string documentName, List<string> failures)
        {
            try
            {
                var result = import(documentName);
                var failure = DocumentImportFailure(result);

                if (failure != null)
                {
                    failures.Add($"{documentName}: {failure}");
                }

                return ImportedBlocks(result);
            }
            catch (EngineeringNotSupportedException ex)
            {
                // Openness cannot import some blocks at all, one with mixed programming languages among them.
                failures.Add($"{documentName}: not supported ({ex.Message})");
                _logger?.LogWarning(ex, "Importing document {DocumentName} is not supported", documentName);
            }
            catch (Exception ex)
            {
                failures.Add($"{documentName}: import threw ({ex.Message})");
                _logger?.LogError(ex, "ImportFromDocuments failed for {DocumentName}", documentName);
            }

            return Array.Empty<PlcBlock>();
        }

        /// <remarks>
        /// PartialSuccess is a failure too: something in the document did not reach the project, and
        /// TIA Portal's own messages are the only account of what. Success with no block in it is
        /// counted as a failure as well, since the caller asked for a block and has nothing to compile.
        /// Both imports apply this one test, so the single and the bulk tool cannot disagree about the
        /// same document.
        /// </remarks>
        private static string? DocumentImportFailure(DocumentImportResultForBlocks? result)
        {
            if (result == null)
            {
                return "no result returned";
            }

            if (result.State != DocumentResultState.Success)
            {
                return $"result state {result.State}{DescribeMessages(result)}";
            }

            return ImportedBlocks(result).Count == 0 ? "TIA Portal reported success but imported no block" : null;
        }

        private static string DescribeMessages(DocumentImportResult result)
        {
            var messages = result.Messages?
                .Select(logged => logged.Message)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList() ?? new List<string>();

            return messages.Count == 0 ? string.Empty : $" ({string.Join(" | ", messages)})";
        }

        private static List<PlcBlock> ImportedBlocks(DocumentImportResultForBlocks? result)
        {
            return result?.ImportedPlcBlocks?.Where(block => block != null).ToList() ?? new List<PlcBlock>();
        }

        private void LogDocumentImport(int importedCount, IReadOnlyList<string> failures)
        {
            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ImportBlocksFromDocuments imported {importedCount} blocks; {failures.Count} documents failed. First failure: {failures[0]}");
                return;
            }

            _logger?.LogInformation($"ImportBlocksFromDocuments completed successfully. Imported {importedCount} blocks.");
        }
    }
}
