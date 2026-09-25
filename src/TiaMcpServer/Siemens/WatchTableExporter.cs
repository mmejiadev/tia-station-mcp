using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.WatchAndForceTables;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Writes every watch and force table of a program to disk, as the backup a table write takes
    /// first.
    /// </summary>
    /// <remarks>
    /// The repository rule is that every write is preceded by an export of the previous state, and
    /// the force table is the reason it matters more here than anywhere else: Openness cannot
    /// create or delete it, so a row added to it by mistake cannot be undone by deleting the table
    /// and starting again. The exported file is what says which rows were there before.
    ///
    /// <see cref="TagTableExporter"/> has the same shape for tags, and the two are deliberately
    /// not merged: they walk different group types with different compositions, and the shared
    /// part is four lines of file handling.
    /// </remarks>
    public sealed class WatchTableExporter
    {
        private const string Extension = ".xml";

        private readonly ILogger? _logger;

        /// <summary>Creates a watch table exporter.</summary>
        /// <param name="logger">Optional logger.</param>
        public WatchTableExporter(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Exports every watch and force table of a program, groups mirrored as directories.</summary>
        /// <param name="root">The program's watch and force table group.</param>
        /// <param name="directory">Directory to write into. Created if it is not there.</param>
        /// <returns>The files written.</returns>
        /// <exception cref="PortalException">An argument is missing, or a table could not be written.</exception>
        public IReadOnlyList<string> ExportAll(PlcWatchAndForceTableSystemGroup root, string directory)
        {
            if (root == null || string.IsNullOrWhiteSpace(directory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The watch table group and a directory are required");
            }

            var written = new List<string>();

            ExportTables(root.WatchTables, root.ForceTables, directory, written);

            foreach (var group in root.Groups)
            {
                ExportGroup(group, Path.Combine(directory, SnapshotFileName.For(group.Name)), written);
            }

            _logger?.LogInformation("Watch and force tables: {Count} exported to {Directory}", written.Count, directory);

            return written;
        }

        /// <summary>Exports one watch table to a file.</summary>
        /// <param name="table">The table to export.</param>
        /// <param name="exportPath">The file to write. Its directory is created if it is not there.</param>
        /// <returns>The full path of the file written.</returns>
        /// <exception cref="PortalException">An argument is missing, or the table could not be written.</exception>
        /// <remarks>
        /// The document this writes is the only specification of the format there is: Siemens ships
        /// no schema for watch tables beside the ones for blocks and interfaces. A table exported
        /// from a project that has the rows somebody wants is what an import is built from.
        /// </remarks>
        public static string ExportOne(PlcWatchTable table, string exportPath)
        {
            if (table == null || string.IsNullOrWhiteSpace(exportPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The table and an export path are required");
            }

            var file = new FileInfo(exportPath);

            Directory.CreateDirectory(file.DirectoryName);

            // Export refuses to write over an existing file.
            if (file.Exists)
            {
                file.Delete();
            }

            table.Export(file, ExportOptions.WithDefaults);

            return file.FullName;
        }

        private static void ExportGroup(PlcWatchAndForceTableUserGroup group, string directory, List<string> written)
        {
            ExportTables(group.WatchTables, group.ForceTables, directory, written);

            foreach (var child in group.Groups)
            {
                ExportGroup(child, Path.Combine(directory, SnapshotFileName.For(child.Name)), written);
            }
        }

        private static void ExportTables(
            PlcWatchTableComposition watchTables,
            PlcForceTableComposition forceTables,
            string directory,
            List<string> written)
        {
            foreach (var table in watchTables)
            {
                written.Add(ExportTable(file => table.Export(file, ExportOptions.WithDefaults), table.Name, directory));
            }

            foreach (var table in forceTables)
            {
                written.Add(ExportTable(file => table.Export(file, ExportOptions.WithDefaults), table.Name, directory));
            }
        }

        /// <remarks>
        /// A failed backup is not swallowed. The whole point of taking one is that the write after
        /// it can be undone, so a write that proceeded on a backup that silently did not happen
        /// would be worse than one that never claimed to have a backup at all.
        /// </remarks>
        private static string ExportTable(System.Action<FileInfo> export, string name, string directory)
        {
            Directory.CreateDirectory(directory);

            var file = new FileInfo(Path.Combine(directory, SnapshotFileName.For(name) + Extension));

            // Export refuses to write over an existing file.
            if (file.Exists)
            {
                file.Delete();
            }

            export(file);

            return file.FullName;
        }
    }
}
