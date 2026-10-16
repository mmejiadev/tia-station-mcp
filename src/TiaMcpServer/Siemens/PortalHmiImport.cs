using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Classic WinCC panels, written: importing screens, HMI tag tables and text lists from XML.
    ///
    /// An import replaces an item of the same name whole, so every one of these exports the whole
    /// panel to the backup directory first and writes nothing when that export is incomplete, nor
    /// when the document links to something the panel lacks (see <c>RequireKnownLinks</c>). The
    /// items link to tags, screens and functions by name, and compiling the HMI is what checks those
    /// links, so a write is followed by CompileSoftware on the HMI path.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Imports a screen into a classic WinCC panel, replacing one of the same name.</summary>
        /// <param name="hmiPath">Full path to the HMI target.</param>
        /// <param name="folderPath">Screen folder to import into, or empty for the root.</param>
        /// <param name="importPath">The screen's XML file.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated for this change.</param>
        /// <returns>The names of the screens imported.</returns>
        /// <exception cref="PortalException">
        /// A path does not resolve, the file is missing, the backup was incomplete so nothing was
        /// imported, or TIA Portal rejected the XML.
        /// </exception>
        public IReadOnlyList<string> ImportHmiScreen(string hmiPath, string folderPath, string importPath, string backupDirectory)
        {
            _logger?.LogInformation("Importing screen {ImportPath} into {HmiPath}/{FolderPath}...", importPath, hmiPath, folderPath);

            try
            {
                var file = RequireImportFile(importPath);
                var hmi = RequireHmiTarget(hmiPath);
                var folder = RequireScreenFolder(hmi, folderPath);

                var exports = SaveHmiBackup(hmi, backupDirectory);
                var xml = File.ReadAllText(file.FullName);
                RequireFreeNumbers(xml, exports);
                RequireKnownLinks(hmi, xml, exports);

                return RequireImported(folder.Screens.Import(file, ImportOptions.Override).Select(screen => screen.Name), importPath);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "ImportHmiScreen"), "ImportHmiScreen", hmiPath, importPath);
            }
        }

        /// <summary>Imports an HMI tag table into a classic WinCC panel, replacing one of the same name.</summary>
        /// <param name="hmiPath">Full path to the HMI target.</param>
        /// <param name="importPath">The tag table's XML file.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated for this change.</param>
        /// <returns>The names of the tables imported.</returns>
        /// <exception cref="PortalException">As for <see cref="ImportHmiScreen"/>.</exception>
        public IReadOnlyList<string> ImportHmiTagTable(string hmiPath, string importPath, string backupDirectory)
        {
            _logger?.LogInformation("Importing HMI tag table {ImportPath} into {HmiPath}...", importPath, hmiPath);

            try
            {
                var file = RequireImportFile(importPath);
                var hmi = RequireHmiTarget(hmiPath);

                var exports = SaveHmiBackup(hmi, backupDirectory);
                RequireKnownLinks(hmi, File.ReadAllText(file.FullName), exports);

                return RequireImported(hmi.TagFolder.TagTables.Import(file, ImportOptions.Override).Select(table => table.Name), importPath);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "ImportHmiTagTable"), "ImportHmiTagTable", hmiPath, importPath);
            }
        }

        /// <summary>Imports a text list into a classic WinCC panel, replacing one of the same name.</summary>
        /// <param name="hmiPath">Full path to the HMI target.</param>
        /// <param name="importPath">The text list's XML file.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated for this change.</param>
        /// <returns>The names of the text lists imported.</returns>
        /// <exception cref="PortalException">As for <see cref="ImportHmiScreen"/>.</exception>
        public IReadOnlyList<string> ImportHmiTextList(string hmiPath, string importPath, string backupDirectory)
        {
            _logger?.LogInformation("Importing text list {ImportPath} into {HmiPath}...", importPath, hmiPath);

            try
            {
                var file = RequireImportFile(importPath);
                var hmi = RequireHmiTarget(hmiPath);

                var exports = SaveHmiBackup(hmi, backupDirectory);
                RequireKnownLinks(hmi, File.ReadAllText(file.FullName), exports);

                return RequireImported(hmi.TextLists.Import(file, ImportOptions.Override).Select(list => list.Name), importPath);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "ImportHmiTextList"), "ImportHmiTextList", hmiPath, importPath);
            }
        }

        /// <summary>Exports the whole panel as the previous state of a write, refusing if any of it was not saved.</summary>
        /// <returns>The export, read back once for the checks that need it.</returns>
        private IReadOnlyList<string> SaveHmiBackup(HmiTarget hmi, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this write replaces HMI items of the same name");
            }

            var report = HmiExporter.ExportAll(hmi, backupDirectory, _logger);

            if (report.Failures.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"Nothing was written: the backup of the HMI is incomplete. {report.Failures.Count} item(s) could not be saved: {string.Join("; ", report.Failures)}");
            }

            return PanelExports(backupDirectory);
        }

        private static ScreenFolder RequireScreenFolder(HmiTarget hmi, string folderPath)
        {
            ScreenFolder folder = hmi.ScreenFolder;

            foreach (var segment in ProjectPath.GroupSegments(folderPath))
            {
                folder = folder.Folders.FirstOrDefault(candidate => candidate.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Screen folder not found: '{folderPath}'");
            }

            return folder;
        }

        private static List<string> RequireImported(IEnumerable<string> names, string importPath)
        {
            var imported = names.ToList();

            return imported.Count > 0
                ? imported
                : throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal imported nothing from '{importPath}'");
        }

    }
}
