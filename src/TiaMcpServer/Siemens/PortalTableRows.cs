using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.WatchTables;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Rows in watch tables and in the force table, which Openness cannot create one by one: they are written as a
    /// document (<see cref="WatchTableDocument"/>) and imported, which replaces every row of that table. See
    /// <see cref="WatchTableRowRules"/> for what was measured on V21 and why the checks are this server's.
    /// </remarks>
    public partial class Portal
    {
        private const string WrittenDocumentName = "written-table.xml";

        /// <summary>Replaces the rows of the program's force table.</summary>
        /// <param name="softwarePath">The PLC's software, for example <c>PLC_1</c>.</param>
        /// <param name="rowsJson">The rows, as <see cref="WatchTableRowReader"/> reads them.</param>
        /// <param name="backupDirectory">Where every table of the program is exported first. Required.</param>
        /// <returns>The force table's rows, read back.</returns>
        /// <exception cref="PortalException">
        /// A row that is refused, no project or PLC, TIA Portal online with the CPU, or TIA Portal refused the import.
        /// </exception>
        /// <remarks>
        /// Offline only: the rows are prepared, not forced. Forcing is done online in TIA Portal, where the box beside
        /// each row is ticked; Openness cannot tick it.
        /// </remarks>
        public IReadOnlyList<WatchEntryInfo> WriteForceTable(string softwarePath, string rowsJson, string backupDirectory)
        {
            _logger?.LogInformation("Writing the force table of {SoftwarePath}...", softwarePath);

            try
            {
                var rows = WatchTableRowReader.Parse(rowsJson, WatchTableKind.Force);
                var software = RequireSoftwareForTableWrite(softwarePath, backupDirectory);
                var forceTables = software.WatchAndForceTableGroup.ForceTables;
                var table = forceTables.FirstOrDefault()
                    ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{softwarePath}' has no force table; TIA Portal makes one with every PLC program");
                var document = WriteDocument(WatchTableKind.Force, table.Name, rows, backupDirectory);

                forceTables.Import(new FileInfo(document), ImportOptions.Override);

                return RequireAllRows(WatchTableReader.ReadEntries(forceTables.Find(table.Name)!), rows.Count, backupDirectory);
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, "force table", "WriteForceTable");
            }
        }

        /// <summary>Writes a watch table at the program's root with exactly these rows, creating it or replacing its rows.</summary>
        /// <param name="softwarePath">The PLC's software, for example <c>PLC_1</c>.</param>
        /// <param name="tableName">The table's name.</param>
        /// <param name="rowsJson">The rows, as <see cref="WatchTableRowReader"/> reads them.</param>
        /// <param name="backupDirectory">Where every table of the program is exported first. Required.</param>
        /// <returns>The table's rows, read back.</returns>
        /// <exception cref="PortalException">
        /// A row that is refused, no name, no project or PLC, TIA Portal online with the CPU, or TIA Portal refused the import.
        /// </exception>
        public IReadOnlyList<WatchEntryInfo> WriteWatchTable(string softwarePath, string tableName, string rowsJson, string backupDirectory)
        {
            _logger?.LogInformation("Writing watch table {TableName} of {SoftwarePath}...", tableName, softwarePath);

            try
            {
                var name = (tableName ?? string.Empty).Trim();

                if (name.Length == 0 || name.Contains("/"))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "tableName is a name at the program's root, e.g. 'Motor checks'");
                }

                var rows = WatchTableRowReader.Parse(rowsJson, WatchTableKind.Watch);
                var software = RequireSoftwareForTableWrite(softwarePath, backupDirectory);
                var watchTables = software.WatchAndForceTableGroup.WatchTables;
                var document = WriteDocument(WatchTableKind.Watch, name, rows, backupDirectory);

                watchTables.Import(new FileInfo(document), ImportOptions.Override);

                return RequireAllRows(WatchTableReader.ReadEntries(watchTables.Find(name)!), rows.Count, backupDirectory);
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, tableName ?? string.Empty, "WriteWatchTable");
            }
        }

        /// <remarks>
        /// TIA Portal can take a document and keep fewer rows than it held, without a word: one row on a data block that
        /// does not exist left the table empty (measured 2026-11-13). The rows are counted back, so that is never
        /// reported as done.
        /// </remarks>
        private static IReadOnlyList<WatchEntryInfo> RequireAllRows(IReadOnlyList<WatchEntryInfo> readBack, int written, string backupDirectory)
        {
            if (readBack.Count == written)
            {
                return readBack;
            }

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"TIA Portal kept {readBack.Count} of the {written} rows written, without saying why, and the table now holds those. "
                + $"The table before is in '{backupDirectory}'. Check the addresses exist in the program.");
        }

        /// <remarks>Kept in the backup directory, so the document that was imported stays beside the tables it replaced.</remarks>
        private static string WriteDocument(WatchTableKind kind, string tableName, IReadOnlyList<WatchTableRow> rows, string backupDirectory)
        {
            var path = Path.Combine(backupDirectory, kind.ToString().ToLowerInvariant() + "-" + WrittenDocumentName);

            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(path, WatchTableDocument.Build(kind, tableName, rows, Openness.BuiltForTiaMajorVersion), new UTF8Encoding(true));

            return path;
        }
    }
}
