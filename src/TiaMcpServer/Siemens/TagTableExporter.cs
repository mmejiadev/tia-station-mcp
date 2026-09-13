using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.Tags;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Writes every tag table of a program to disk, as the backup a tag write takes first.
    /// </summary>
    /// <remarks>
    /// The repository rule is that every write is preceded by an export of the previous state, and
    /// for tags that state is the whole set of tables: creating one entry can be undone by reading
    /// it, but a caller that then finds a name it did not expect needs the tables as they were.
    /// <c>WriteScl</c> takes the same shape of backup for blocks.
    ///
    /// XML because TIA Portal offers nothing else for a tag table. It still diffs acceptably, since
    /// one table is one file.
    /// </remarks>
    public sealed class TagTableExporter
    {
        private const string Extension = ".xml";

        private readonly ILogger? _logger;

        /// <summary>Creates a tag table exporter.</summary>
        /// <param name="logger">Optional logger.</param>
        public TagTableExporter(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Exports every tag table of a program, groups mirrored as directories.</summary>
        /// <param name="root">The program's tag table group.</param>
        /// <param name="directory">Directory to write into. Created if it is not there.</param>
        /// <returns>The files written.</returns>
        /// <exception cref="PortalException">An argument is missing, or a table could not be written.</exception>
        public IReadOnlyList<string> ExportAll(PlcTagTableGroup root, string directory)
        {
            if (root == null || string.IsNullOrWhiteSpace(directory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The tag table group and a directory are required");
            }

            var written = new List<string>();

            Export(root, directory, written);

            _logger?.LogInformation("Tag tables: {Count} exported to {Directory}", written.Count, directory);

            return written;
        }

        private static void Export(PlcTagTableGroup group, string directory, List<string> written)
        {
            foreach (var table in group.TagTables)
            {
                written.Add(ExportTable(table, directory));
            }

            foreach (var nested in group.Groups)
            {
                Export(nested, Path.Combine(directory, SnapshotFileName.For(nested.Name)), written);
            }
        }

        /// <remarks>
        /// A failed backup is not swallowed. The whole point of taking one is that the write after
        /// it can be undone, so a write that proceeded on a backup that silently did not happen
        /// would be worse than one that never claimed to have a backup at all.
        /// </remarks>
        private static string ExportTable(PlcTagTable table, string directory)
        {
            Directory.CreateDirectory(directory);

            var file = new FileInfo(Path.Combine(directory, SnapshotFileName.For(table.Name) + Extension));

            // Export refuses to write over an existing file.
            if (file.Exists)
            {
                file.Delete();
            }

            table.Export(file, ExportOptions.WithDefaults);

            return file.FullName;
        }
    }
}
