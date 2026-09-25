using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Watch and force tables: which addresses somebody will be looking at, and what is queued up
    /// to be done to them.
    ///
    /// All of it is the offline project. Openness creates these tables and fills them in and never
    /// reads what a CPU holds, so nothing here reports a live value and nothing here changes a
    /// machine. A prepared row takes effect when a person opens the table against a connected
    /// controller and activates it — which is the moment worth naming, because it is the only one
    /// where a force becomes physical.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Lists the watch and force tables of a PLC program.</summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>PLC_0</c>.</param>
        /// <returns>The tables, those in user groups named by their group path.</returns>
        /// <exception cref="PortalException">No project is open, or the software does not resolve.</exception>
        public IReadOnlyList<WatchTableInfo> GetWatchTables(string softwarePath)
        {
            _logger?.LogInformation("Reading the watch and force tables of {SoftwarePath}...", softwarePath);

            try
            {
                return WatchTableReader.ReadTables(RequireSoftwareWithTables(softwarePath));
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, string.Empty, "GetWatchTables");
            }
        }

        /// <summary>Reads the rows of one watch or force table.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="tablePath">The table, <c>Group/Name</c> or just <c>Name</c>.</param>
        /// <returns>Its rows, in the order the table holds them.</returns>
        /// <exception cref="PortalException">No project is open, or there is no such table.</exception>
        /// <remarks>
        /// One tool for both kinds, because a caller holding a name from GetWatchTables should not
        /// have to know which kind it got before it can read it.
        /// </remarks>
        public IReadOnlyList<WatchEntryInfo> GetWatchTable(string softwarePath, string tablePath)
        {
            _logger?.LogInformation("Reading the rows of {TablePath} in {SoftwarePath}...", tablePath, softwarePath);

            try
            {
                var software = RequireSoftwareWithTables(softwarePath);

                if (WatchTableReader.FindWatchTable(software, tablePath) is { } watched)
                {
                    return WatchTableReader.ReadEntries(watched);
                }

                if (WatchTableReader.FindForceTable(software, tablePath) is { } forced)
                {
                    return WatchTableReader.ReadEntries(forced);
                }

                throw new PortalException(PortalErrorCode.NotFound, $"No watch or force table called '{tablePath}'");
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, tablePath, "GetWatchTable");
            }
        }

        /// <summary>Creates a watch table in a PLC program.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="tablePath">The table, <c>Group/Name</c> or just <c>Name</c> at the root.</param>
        /// <param name="backupDirectory">Where the current tables are exported first. Required.</param>
        /// <returns>The table as the project holds it.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the group does not exist, or a table of that name is already there.
        /// </exception>
        /// <remarks>
        /// The group has to exist. Creating one implicitly would mean a misspelt group path
        /// silently produced a second folder holding one table, which is the same failure
        /// <c>CreateTag</c> refuses for tag tables.
        /// </remarks>
        public WatchTableInfo CreateWatchTable(string softwarePath, string tablePath, string backupDirectory)
        {
            _logger?.LogInformation("Creating watch table {TablePath} in {SoftwarePath}...", tablePath, softwarePath);

            try
            {
                var software = RequireSoftwareForTableWrite(softwarePath, backupDirectory);
                var target = ProjectPath.Parse(tablePath);
                var group = RequireGroup(software, target.Parent);

                var table = WatchTableWriter.CreateTable(group, target.Name);

                return new WatchTableInfo(tablePath, WatchTableInfo.WatchKind, table.Entries.Count, table.IsConsistent);
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, tablePath, "CreateWatchTable");
            }
        }

        /// <summary>Exports one watch table to a file.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="tablePath">The watch table, as GetWatchTables names it.</param>
        /// <param name="exportPath">The file to write.</param>
        /// <returns>The full path of the file written.</returns>
        /// <exception cref="PortalException">No project is open, or there is no such watch table.</exception>
        /// <remarks>
        /// The document is the only way rows get into a table, so this is half of the pair rather
        /// than a convenience: a table with the rows somebody wants is exported here and imported
        /// wherever it is needed.
        /// </remarks>
        public string ExportWatchTable(string softwarePath, string tablePath, string exportPath)
        {
            _logger?.LogInformation("Exporting watch table {TablePath} to {ExportPath}...", tablePath, exportPath);

            try
            {
                var software = RequireSoftwareWithTables(softwarePath);

                var table = WatchTableReader.FindWatchTable(software, tablePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"No watch table called '{tablePath}'");

                return WatchTableExporter.ExportOne(table, exportPath);
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, tablePath, "ExportWatchTable");
            }
        }

        /// <summary>Imports watch tables into a PLC program from a document.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="documentPath">The document, as ExportWatchTable writes one.</param>
        /// <param name="backupDirectory">Where the current tables are exported first. Required.</param>
        /// <returns>The names of the tables the import produced.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the document is not there, or TIA Portal refused it.
        /// </exception>
        /// <remarks>
        /// Tables land at the program's root. Importing into a group is left out rather than
        /// guessed at: the document names the table, and where a group path would win over what
        /// the document says is a decision with no obvious answer.
        /// </remarks>
        public IReadOnlyList<string> ImportWatchTables(string softwarePath, string documentPath, string backupDirectory)
        {
            _logger?.LogInformation("Importing watch tables from {DocumentPath} into {SoftwarePath}...", documentPath, softwarePath);

            try
            {
                var software = RequireSoftwareForTableWrite(softwarePath, backupDirectory);

                return WatchTableWriter.ImportTables(software.WatchAndForceTableGroup, new FileInfo(documentPath));
            }
            catch (Exception ex)
            {
                throw DecorateTableFailure(ex, softwarePath, documentPath, "ImportWatchTables");
            }
        }

        private PlcSoftware RequireSoftwareWithTables(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading watch tables");
            }

            var container = GetSoftwareContainer(softwarePath);

            return container?.Software as PlcSoftware
                ?? throw new PortalException(PortalErrorCode.NotFound, $"PLC software not found: {softwarePath}");
        }

        /// <summary>The software a table write targets, with its tables exported before anything changes.</summary>
        private PlcSoftware RequireSoftwareForTableWrite(string softwarePath, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes the program's tables");
            }

            var software = RequireSoftwareWithTables(softwarePath);

            new WatchTableExporter(_logger).ExportAll(software.WatchAndForceTableGroup, backupDirectory);

            return software;
        }

        private static PlcWatchAndForceTableGroup RequireGroup(PlcSoftware software, string groupPath)
        {
            PlcWatchAndForceTableGroup group = software.WatchAndForceTableGroup;

            foreach (var segment in ProjectPath.GroupSegments(groupPath))
            {
                group = WatchTableReader.FindUserGroup(group, segment)
                    ?? throw new PortalException(
                        PortalErrorCode.NotFound,
                        $"No watch table group called '{segment}' in '{groupPath}'. Create it in TIA Portal, or leave the group out to put the table at the root.");
            }

            return group;
        }

        private PortalException DecorateTableFailure(Exception ex, string softwarePath, string tablePath, string methodName)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{methodName} failed: {ex.Message}", null, ex);

            pex.Data["softwarePath"] = softwarePath;
            pex.Data["tablePath"] = tablePath;

            _logger?.LogError(pex, "{MethodName} failed for {SoftwarePath} {TablePath}", methodName, softwarePath, tablePath);

            return pex;
        }
    }
}
