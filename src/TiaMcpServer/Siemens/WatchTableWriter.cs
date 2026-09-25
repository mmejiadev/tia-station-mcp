using Siemens.Engineering;
using Siemens.Engineering.SW.WatchAndForceTables;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Creates watch tables, and imports tables from a document.
    /// </summary>
    /// <remarks>
    /// What this class does not do is the measurement that shaped it. **A row with an address
    /// cannot be created through Openness at all.** Asked what it will make in a table's
    /// <c>Entries</c>, TIA Portal V20 answers with one type and it is not a watch row:
    ///
    /// <code>does not offer to create a PlcWatchTableEntry in 'Entries'. It offers: PlcTableCommentEntry</code>
    ///
    /// Measured on 2026-09-22, after the generic composition create had already been tried and
    /// refused outright. So rows arrive the way LAD blocks do: as a document that is imported.
    /// <see cref="WatchTableExporter"/> writes one out, <see cref="ImportTables"/> reads one back,
    /// and Siemens publishes no schema for the format — the exported file is the specification.
    ///
    /// The other asymmetry: a watch table is created and deleted freely, and the force table is
    /// neither. Its composition offers <c>Find</c> and <c>Import</c> and no <c>Create</c>, because
    /// a PLC program has exactly one and TIA Portal makes it.
    /// </remarks>
    public static class WatchTableWriter
    {
        /// <summary>Creates a watch table in a group of a PLC program.</summary>
        /// <param name="group">The group it goes in, the program's root or one below it.</param>
        /// <param name="name">The table's name.</param>
        /// <returns>The table that was created.</returns>
        /// <exception cref="PortalException">A table of that name is already there.</exception>
        public static PlcWatchTable CreateTable(PlcWatchAndForceTableGroup group, string name)
        {
            var tables = WatchTableReader.WatchTablesOf(group);

            if (tables.Find(name) != null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"A watch table called '{name}' is already there. Nothing was replaced.");
            }

            return tables.Create(name);
        }

        /// <summary>Imports watch tables into a group from a SimaticML document.</summary>
        /// <param name="group">The group they go into, the program's root or one below it.</param>
        /// <param name="document">The document to import, as ExportWatchTable writes one.</param>
        /// <returns>The names of the tables the import produced.</returns>
        /// <exception cref="PortalException">The document is not there, or TIA Portal refused it.</exception>
        /// <remarks>
        /// Override, like the block and type imports: a table of the same name is replaced rather
        /// than duplicated. What the replaced one held is in the export taken before this runs,
        /// which is the whole reason that export is not optional.
        /// </remarks>
        public static IReadOnlyList<string> ImportTables(PlcWatchAndForceTableGroup group, FileInfo document)
        {
            if (document == null || !document.Exists)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"No such document: {document?.FullName}");
            }

            var imported = WatchTableReader.WatchTablesOf(group).Import(document, ImportOptions.Override);

            return imported.Select(table => table.Name).ToList();
        }
    }
}
