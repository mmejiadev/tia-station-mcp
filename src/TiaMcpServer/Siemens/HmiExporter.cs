using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Exports what a classic WinCC panel holds — screens, templates, HMI tag tables, text and
    /// graphic lists — to XML, one file per item.
    /// </summary>
    /// <remarks>
    /// Classic WinCC has no object model for what is drawn on a screen (measured 2026-10-13): a
    /// button, its events and its animations exist for Openness only in the exported XML. So this
    /// export is both the way to read a panel and the backup an HMI write takes first.
    ///
    /// Folders are mirrored as directories. A file name two items would share is a failure, not an
    /// overwrite: <see cref="SnapshotFileName"/> maps names lossily, and a backup that quietly lost
    /// one of them would claim to be complete while it was not.
    /// </remarks>
    internal sealed class HmiExporter
    {
        private const string Extension = ".xml";

        private readonly string _root;
        private readonly ILogger? _logger;
        private readonly List<string> _written = new List<string>();
        private readonly List<string> _failures = new List<string>();

        private HmiExporter(string root, ILogger? logger)
        {
            _root = root;
            _logger = logger;
        }

        /// <summary>Exports everything the panel holds.</summary>
        /// <param name="hmi">The panel's HMI target.</param>
        /// <param name="directory">The directory to write into; created when missing.</param>
        /// <param name="logger">Where each failure is logged with its exception.</param>
        /// <returns>The files written, and one line per item that could not be.</returns>
        public static ExportReport<string> ExportAll(HmiTarget hmi, string directory, ILogger? logger)
        {
            var exporter = new HmiExporter(directory, logger);

            exporter.ExportScreens(hmi.ScreenFolder, "Screens");
            exporter.ExportTemplates(hmi.ScreenTemplateFolder, "Templates");
            exporter.ExportTagTables(hmi.TagFolder, "Tags");
            exporter.ExportLists(hmi);

            return new ExportReport<string>(exporter._written, exporter._failures);
        }

        private void ExportScreens(ScreenFolder folder, string relativePath)
        {
            foreach (var screen in folder.Screens)
            {
                Attempt(relativePath, screen.Name, file => screen.Export(file, ExportOptions.WithDefaults));
            }

            foreach (var subfolder in folder.Folders)
            {
                ExportScreens(subfolder, Path.Combine(relativePath, SnapshotFileName.For(subfolder.Name)));
            }
        }

        private void ExportTemplates(ScreenTemplateFolder folder, string relativePath)
        {
            foreach (var template in folder.ScreenTemplates)
            {
                Attempt(relativePath, template.Name, file => template.Export(file, ExportOptions.WithDefaults));
            }

            foreach (var subfolder in folder.Folders)
            {
                ExportTemplates(subfolder, Path.Combine(relativePath, SnapshotFileName.For(subfolder.Name)));
            }
        }

        private void ExportTagTables(TagFolder folder, string relativePath)
        {
            foreach (var table in folder.TagTables)
            {
                Attempt(relativePath, table.Name, file => table.Export(file, ExportOptions.WithDefaults));
            }

            foreach (var subfolder in folder.Folders)
            {
                ExportTagTables(subfolder, Path.Combine(relativePath, SnapshotFileName.For(subfolder.Name)));
            }
        }

        private void ExportLists(HmiTarget hmi)
        {
            foreach (var list in hmi.TextLists)
            {
                Attempt("Text lists", list.Name, file => list.Export(file, ExportOptions.WithDefaults));
            }

            foreach (var list in hmi.GraphicLists)
            {
                Attempt("Graphic lists", list.Name, file => list.Export(file, ExportOptions.WithDefaults));
            }
        }

        /// <remarks>
        /// One item failing to export is recorded and the export goes on. The session ending is not an
        /// item failing: after <see cref="NonRecoverableException"/> the project is disposed (measured
        /// 2026-10-14), so those propagate, to be reported as the session ending, and so do the
        /// failures of the process itself.
        /// </remarks>
        private static bool IsItemFailure(Exception ex)
        {
            return !(ex is NonRecoverableException
                || ex is EngineeringObjectDisposedException
                || ex is OutOfMemoryException
                || ex is StackOverflowException);
        }

        /// <remarks>
        /// A failure is recorded, not swallowed: the read tool reports every line, and the backup
        /// refuses the write when there is any. One item failing is not allowed to stop the others,
        /// so the caller learns about all of them at once.
        /// </remarks>
        private void Attempt(string relativePath, string name, Action<FileInfo> export)
        {
            try
            {
                var directory = Path.Combine(_root, relativePath);
                Directory.CreateDirectory(directory);

                var file = new FileInfo(Path.Combine(directory, SnapshotFileName.For(name) + Extension));

                if (file.Exists)
                {
                    throw new PortalException(PortalErrorCode.ExportFailed, $"another item already wrote {file.Name}; their names differ only in characters a file name cannot hold");
                }

                export(file);
                _written.Add(file.FullName);
            }
            catch (Exception ex) when (IsItemFailure(ex))
            {
                _failures.Add($"{relativePath}/{name}: {ex.Message}");
                _logger?.LogError(ex, "Could not export HMI item {Path}/{Name}", relativePath, name);
            }
        }
    }
}
