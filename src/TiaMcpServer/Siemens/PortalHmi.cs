using Microsoft.Extensions.Logging;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Classic WinCC panels (Basic, Comfort, Advanced — the <c>HmiTarget</c> of the class projects),
    /// read: what a panel holds, and its export to XML.
    ///
    /// WinCC Unified is a different API and is not covered; a Unified panel is not found here.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Lists what a classic WinCC panel holds, by name.</summary>
        /// <param name="hmiPath">Full path to the HMI target, as GetProjectTree shows it.</param>
        /// <returns>Screens, templates, HMI tag tables with their tag counts, and text lists.</returns>
        /// <exception cref="PortalException">No project is open, or there is no classic HMI at the path.</exception>
        public HmiSummary GetHmiInfo(string hmiPath)
        {
            _logger?.LogInformation("Reading the HMI at {HmiPath}...", hmiPath);

            try
            {
                var hmi = RequireHmiTarget(hmiPath);

                return new HmiSummary(
                    ListScreens(hmi.ScreenFolder, string.Empty),
                    ListTemplates(hmi.ScreenTemplateFolder, string.Empty),
                    ListTagTables(hmi.TagFolder, string.Empty),
                    hmi.TextLists.Select(list => list.Name).ToList());
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetHmiInfo", hmiPath, string.Empty);
            }
        }

        /// <summary>Exports everything a classic WinCC panel holds to XML, one file per item.</summary>
        /// <param name="hmiPath">Full path to the HMI target.</param>
        /// <param name="exportDirectory">An empty or missing directory; folders are mirrored below it.</param>
        /// <returns>The files written, and one line per item that could not be.</returns>
        /// <exception cref="PortalException">
        /// No project is open, there is no classic HMI at the path, or the directory already holds files.
        /// </exception>
        public ExportReport<string> ExportHmi(string hmiPath, string exportDirectory)
        {
            _logger?.LogInformation("Exporting the HMI at {HmiPath} to {ExportDirectory}...", hmiPath, exportDirectory);

            try
            {
                RequireEmptyDirectory(exportDirectory);

                return HmiExporter.ExportAll(RequireHmiTarget(hmiPath), exportDirectory, _logger);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "ExportHmi", hmiPath, exportDirectory);
            }
        }

        /// <summary>Whether a path names a classic WinCC HMI target rather than a PLC program.</summary>
        /// <param name="softwarePath">The path, as the tools take it.</param>
        /// <returns>True for an HMI target; false for anything else, or when no project is open.</returns>
        public bool IsHmiTarget(string softwarePath)
        {
            return !IsProjectNull() && GetSoftwareContainer(softwarePath)?.Software is HmiTarget;
        }

        private HmiTarget RequireHmiTarget(string hmiPath)
        {
            if (string.IsNullOrWhiteSpace(hmiPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "hmiPath is required");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before working with an HMI");
            }

            return GetSoftwareContainer(hmiPath)?.Software as HmiTarget
                ?? throw new PortalException(PortalErrorCode.NotFound, DescribeMissingHmi(hmiPath));
        }

        /// <remarks>
        /// Names the HMI targets that do exist. A panel's target sits on a device item TIA Portal
        /// numbers itself, so the path a caller tries first is usually the device's name, and the
        /// refusal is the place to learn the right one.
        /// </remarks>
        private string DescribeMissingHmi(string hmiPath)
        {
            var paths = GetHmiTargetPaths();
            var known = paths.Count == 0 ? "the project has none" : "the project has: " + string.Join(", ", paths);

            return $"No classic WinCC HMI found at '{hmiPath}' (WinCC Unified panels are not supported); {known}";
        }

        /// <remarks>
        /// An export into a directory that already holds one would mix two panels' files, and the
        /// exporter refuses a file name it finds taken; refusing the directory says why up front.
        /// </remarks>
        private static void RequireEmptyDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "exportDirectory is required");
            }

            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{directory}' is not empty; export into a new directory");
            }
        }

        private static List<string> ListScreens(ScreenFolder folder, string prefix)
        {
            var names = folder.Screens.Select(screen => prefix + screen.Name).ToList();

            foreach (var subfolder in folder.Folders)
            {
                names.AddRange(ListScreens(subfolder, $"{prefix}{subfolder.Name}/"));
            }

            return names;
        }

        private static List<string> ListTemplates(ScreenTemplateFolder folder, string prefix)
        {
            var names = folder.ScreenTemplates.Select(template => prefix + template.Name).ToList();

            foreach (var subfolder in folder.Folders)
            {
                names.AddRange(ListTemplates(subfolder, $"{prefix}{subfolder.Name}/"));
            }

            return names;
        }

        private static List<string> ListTagTables(TagFolder folder, string prefix)
        {
            var names = folder.TagTables.Select(table => $"{prefix}{table.Name} ({table.Tags.Count} tags)").ToList();

            foreach (var subfolder in folder.Folders)
            {
                names.AddRange(ListTagTables(subfolder, $"{prefix}{subfolder.Name}/"));
            }

            return names;
        }

        private PortalException DecorateHmiFailure(Exception ex, string operation, string hmiPath, string filePath)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["hmiPath"] = hmiPath;
            pex.Data["filePath"] = filePath;

            _logger?.LogError(pex, "{Operation} failed for {HmiPath} {FilePath}", operation, hmiPath, filePath);
            return pex;
        }
    }
}
