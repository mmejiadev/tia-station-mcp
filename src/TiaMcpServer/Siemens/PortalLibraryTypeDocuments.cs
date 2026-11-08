using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.Types;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Library types of blocks and PLC data types made from SIMATIC SD documents, measured 2026-11-08 on TIA Portal V20: see
    /// <see cref="LibraryTypeDocumentRules"/>. A type's only version discarded takes the type and its instance with it, and a
    /// later one discarded puts the instance back on the version before, so a document that does not compile leaves nothing.
    /// </remarks>
    public partial class Portal
    {
        private const string DocumentBeforeFolder = "document-before";

        /// <summary>Creates a project library type from a document, with its first version in work and an instance in the program.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="groupPath">The block or data type group the instance goes in; empty for the root.</param>
        /// <param name="importPath">The folder holding the document, a full path.</param>
        /// <param name="documentName">The document's name without its extension, which is also what it must declare.</param>
        /// <returns>The type, read back; <c>compileWarnings</c>; and <c>warning</c> when TIA Portal had one.</returns>
        /// <exception cref="PortalException">See <see cref="LibraryTypeDocumentRules"/>; or the group or document is missing, or the document does not compile.</exception>
        /// <remarks>Nothing existing changes, so there is no earlier state to keep; what does not compile is taken back.</remarks>
        public JsonObject CreateLibraryTypeFromDocument(string softwarePath, string groupPath, string importPath, string documentName)
        {
            _logger?.LogInformation("Creating library type from document {Document} in {Software}...", documentName, softwarePath);

            try
            {
                RequireOfflineSoftware(softwarePath);
                var library = RequireProjectLibrary();
                var document = ReadTypeDocument(importPath, documentName);
                LibraryTypeDocumentRules.CheckNameFree(document.Name, BlockAndTypeNames(softwarePath), LibraryTypeReader.ByName(library).Keys);

                var target = RequireTypeDocumentTarget(softwarePath, document.Kind, groupPath ?? string.Empty);
                var result = library.Types.CreateFromDocuments(DocumentFolder(importPath), document.Name, target, LibraryImportOptions.None);
                var created = result.CreatedType ?? throw NotTaken(document.Name, result.Messages);
                var report = CompileTypeInstance(softwarePath, document);

                if (!report.IsSuccessful)
                {
                    throw DoesNotCompile(document.Name, report, UndoNewType(created));
                }

                return DescribeTypeFromDocument(document.Name, report, TransferWarning(result.TransferResultState, result.Messages));
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreateLibraryTypeFromDocument", PortalErrorCode.WriteFailed), "CreateLibraryTypeFromDocument", softwarePath, documentName ?? string.Empty);
            }
        }

        /// <summary>Makes a new version in work of a project library type from a document, or replaces what its version in work holds.</summary>
        /// <param name="request">The PLC software, the folder holding the document named after the type, and where the state before is kept.</param>
        /// <param name="typeName">The type, which must have an instance in that program.</param>
        /// <param name="replaceInWork">True to replace the version in work rather than make a new one.</param>
        /// <returns>The type, read back; <c>compileWarnings</c>; and <c>warning</c> when TIA Portal had one.</returns>
        /// <exception cref="PortalException">See <see cref="LibraryTypeDocumentRules"/>; or the type, its instance or the document is missing, or the document does not compile.</exception>
        /// <remarks>Changes the program too: the instance moves onto the new version. What does not compile is taken back.</remarks>
        public JsonObject CreateLibraryTypeVersionFromDocument(DocumentImportRequest request, string typeName, bool replaceInWork)
        {
            _logger?.LogInformation("Creating a version of library type {Type} from a document...", typeName);

            try
            {
                var software = RequireOfflineSoftware(request.SoftwarePath);
                var found = RequireLibraryType(typeName);
                var document = ReadTypeDocument(request.ImportPath, found.Type.Name);
                RequireDocumentFits(found.Type, document.Kind);
                LibraryTypeDocumentRules.CheckVersion(found.Type.Name, LibraryTypeReader.Versions(found.Type), replaceInWork);
                var instance = RequireTypeInstance(request.SoftwarePath, document, replaceInWork);

                var before = SaveTypeVersionBackup(request, software, found, instance);
                var result = found.Type.Versions.CreateFromDocuments(DocumentFolder(request.ImportPath), document.Name, replaceInWork ? CreateOptions.Override : CreateOptions.None, LibraryImportOptions.None);

                if (result.CreatedVersion == null)
                {
                    throw NotTaken(document.Name, result.Messages);
                }

                var report = CompileTypeInstance(request.SoftwarePath, document);

                if (!report.IsSuccessful)
                {
                    throw DoesNotCompile(document.Name, report, UndoTypeVersion(found.Type, before, replaceInWork));
                }

                return DescribeTypeFromDocument(document.Name, report, TransferWarning(result.TransferResultState, result.Messages));
            }
            catch (Exception ex)
            {
                throw DecorateLibraryFailure(AsLibraryFailure(ex, "CreateLibraryTypeVersionFromDocument", PortalErrorCode.WriteFailed), "CreateLibraryTypeVersionFromDocument", request?.SoftwarePath ?? string.Empty, typeName ?? string.Empty);
            }
        }

        private static (LibraryTypeDocumentKind Kind, string Name) ReadTypeDocument(string importPath, string documentName)
        {
            LibraryTypeDocumentRules.CheckDocumentName(documentName);

            if (string.IsNullOrWhiteSpace(importPath) || !IsFullPath(importPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"importPath must be a full path, as TIA Portal refuses a relative one; got '{importPath}'");
            }

            var file = Path.Combine(importPath, documentName + LibraryTypeDocumentRules.DocumentExtension);
            if (!File.Exists(file))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"No document '{file}'");
            }

            return LibraryTypeDocumentRules.Read(documentName, File.ReadAllText(file, Encoding.UTF8));
        }

        /// <remarks>Measured 2026-11-08: anything but the group of the document's own kind is "The selected target is invalid".</remarks>
        private IEngineeringObject RequireTypeDocumentTarget(string softwarePath, LibraryTypeDocumentKind kind, string groupPath)
        {
            IEngineeringObject? group = kind == LibraryTypeDocumentKind.Block
                ? GetPlcBlockGroupByPath(softwarePath, groupPath)
                : GetPlcTypeGroupByPath(softwarePath, groupPath);

            return group ?? throw new PortalException(PortalErrorCode.NotFound, $"The program '{softwarePath}' has no group '{groupPath}' for this document; create it first, or leave groupPath empty for the root");
        }

        private static void RequireDocumentFits(LibraryType type, LibraryTypeDocumentKind kind)
        {
            var fits = kind == LibraryTypeDocumentKind.Block ? type is CodeBlockLibraryType : type is PlcTypeLibraryType;

            if (!fits)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{type.Name}' is a {type.GetType().Name}, and this document declares a {kind}");
            }
        }

        /// <remarks>
        /// The instance is found afresh each time: one read before a new version is disposed by it (measured 2026-11-08).
        /// Replacing the version in work takes an instance on it: the undo puts back what the instance holds.
        /// </remarks>
        private TypeInstance RequireTypeInstance(string softwarePath, (LibraryTypeDocumentKind Kind, string Name) document, bool replaceInWork)
        {
            var instance = FindTypeInstance(softwarePath, document);
            var linked = instance?.Item.GetService<LibraryTypeInstanceInfo>()?.LibraryTypeVersion;

            if (linked == null || !IsNamed(linked.TypeObject.Name, document.Name))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The program '{softwarePath}' has no instance of '{document.Name}', and TIA Portal makes a new version through one; a version is not made without it");
            }

            if (replaceInWork && LibraryTypeReader.IsReleased(linked))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The instance of '{document.Name}' in '{softwarePath}' is on a released version, so what the version in work holds cannot be kept first; give the program whose instance is on it");
            }

            return instance!;
        }

        private TypeInstance? FindTypeInstance(string softwarePath, (LibraryTypeDocumentKind Kind, string Name) document)
        {
            if (document.Kind == LibraryTypeDocumentKind.Block)
            {
                var block = FindBlocks(softwarePath).FirstOrDefault(candidate => IsNamed(candidate.Name, document.Name));

                return block == null ? null : new TypeInstance(block, block.ExportAsDocuments);
            }

            var type = FindTypes(softwarePath).FirstOrDefault(candidate => IsNamed(candidate.Name, document.Name));

            return type == null ? null : new TypeInstance(type, type.ExportAsDocuments);
        }

        private static bool IsNamed(string name, string expected)
        {
            return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <remarks>An exception here is a failed check like any other: what was imported is taken back the same way.</remarks>
        private CompilationReport CompileTypeInstance(string softwarePath, (LibraryTypeDocumentKind Kind, string Name) document)
        {
            try
            {
                return CompilerResultReader.Read(FindTypeInstance(softwarePath, document)?.Item.GetService<ICompilable>()?.Compile());
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return new CompilationReport(CompilationSeverity.Error, errorCount: 1, warningCount: 0, new[] { new CompilationMessage(CompilationSeverity.Error, string.Empty, failure.Message) });
            }
        }

        private DirectoryInfo SaveTypeVersionBackup(DocumentImportRequest request, PlcSoftware software, (string Folder, LibraryType Type) found, TypeInstance instance)
        {
            ProgramBackup.Save(software, request.BackupDirectory, _logger);
            SaveLibraryBackup(request.BackupDirectory, $"{SnapshotFileName.For(found.Type.Name)}.json", LibraryTypeReader.Describe(found.Folder, found.Type));

            return SaveDocumentBefore(instance, found.Type.Name, request.BackupDirectory);
        }

        /// <remarks>The file a replaced version in work is put back from; a program backup holds SimaticML, which this cannot take.</remarks>
        private static DirectoryInfo SaveDocumentBefore(TypeInstance instance, string name, string backupDirectory)
        {
            var directory = Path.GetFullPath(Path.Combine(backupDirectory, DocumentBeforeFolder));
            Directory.CreateDirectory(directory);
            var exported = instance.ExportDocument(new DirectoryInfo(directory), name);

            if (exported == null || exported.State != DocumentResultState.Success)
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"Nothing was written: '{name}' could not be saved as a document first");
            }

            return new DirectoryInfo(directory);
        }

        /// <returns>What became of it, for the refusal to say; a failure here must not replace the failure being reported.</returns>
        private static string UndoNewType(LibraryType type)
        {
            try
            {
                type.Versions.Single().Discard();
                return "nothing was created";
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return $"taking it back failed too ({failure.Message}): the type and its instance are still there";
            }
        }

        /// <returns>What became of it, for the refusal to say; a failure here must not replace the failure being reported.</returns>
        private static string UndoTypeVersion(LibraryType type, DirectoryInfo before, bool replaceInWork)
        {
            try
            {
                if (!replaceInWork)
                {
                    type.Versions.Single(version => !LibraryTypeReader.IsReleased(version)).Discard();
                    return "the new version was discarded and the instance is back on the one before";
                }

                return type.Versions.CreateFromDocuments(before, type.Name, CreateOptions.Override, LibraryImportOptions.None).CreatedVersion != null
                    ? "the version in work holds what it held"
                    : "putting back what the version in work held did not take: it is in the backup registry";
            }
            catch (Exception failure) when (IsUndoableFailure(failure))
            {
                return $"undoing it failed too ({failure.Message}): the state before is in the backup registry";
            }
        }

        /// <remarks>Not an ended session: that one goes on up, so that the project is forgotten (see <c>ForgetEndedSession</c>).</remarks>
        private static bool IsUndoableFailure(Exception failure)
        {
            return !UnifiedRollback.IsSessionEnded(failure) && (failure is EngineeringException || failure is IOException || failure is InvalidOperationException);
        }

        private static PortalException NotTaken(string name, TransferResultMessageComposition messages)
        {
            return new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal did not take the document '{name}': {string.Join(" ", messages.Select(message => message.Message))}");
        }

        private static PortalException DoesNotCompile(string name, CompilationReport report, string outcome)
        {
            var errors = string.Join("; ", report.Errors.Take(MaxReportedCompileErrors));

            return new PortalException(PortalErrorCode.InvalidParams, $"'{name}' does not compile, and TIA Portal would have taken it without a word; {outcome}. {report.ErrorCount} error(s): {errors}");
        }

        private const int MaxReportedCompileErrors = 10;

        private JsonObject DescribeTypeFromDocument(string name, CompilationReport report, string? warning)
        {
            var found = RequireLibraryType(name);
            var described = LibraryTypeReader.Describe(found.Folder, found.Type);
            described["compileWarnings"] = report.WarningCount;

            return WithWarning(described, warning);
        }

        private static string? TransferWarning(TransferResultState state, TransferResultMessageComposition messages)
        {
            return state == TransferResultState.Success ? null : string.Join(" ", messages.Select(message => message.Message));
        }

        private static DirectoryInfo DocumentFolder(string importPath)
        {
            return new DirectoryInfo(Path.GetFullPath(importPath));
        }

        /// <summary>A block or PLC data type holding a library type: the two share no base with what is needed here.</summary>
        private sealed class TypeInstance
        {
            public TypeInstance(IEngineeringServiceProvider item, Func<DirectoryInfo, string, DocumentExportResult> exportDocument)
            {
                Item = item;
                ExportDocument = exportDocument;
            }

            public IEngineeringServiceProvider Item { get; }

            public Func<DirectoryInfo, string, DocumentExportResult> ExportDocument { get; }
        }
    }
}
