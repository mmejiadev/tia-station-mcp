using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Tag tables: the names a program uses, read and written.
    ///
    /// They have been exported since the snapshot existed and could not be authored until now, so
    /// a program this server generated could only refer to tags somebody had typed into TIA Portal
    /// first. Everything here is aimed with a path — <c>Cell/IO/Start</c> — because a tag's table
    /// is part of its identity and a bare name would land it in whichever table the code picked.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Lists every tag table of a PLC program, by full path.</summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>PLC_0</c>.</param>
        /// <returns>One entry per table, with how much each holds.</returns>
        /// <exception cref="PortalException">No project is open, or the software path does not resolve.</exception>
        public IReadOnlyList<TagTableInfo> GetTagTables(string softwarePath)
        {
            _logger?.LogInformation("Reading the tag tables of {SoftwarePath}...", softwarePath);

            try
            {
                var software = RequireSoftware(softwarePath);

                return new TagTableReader(_logger).ReadTables(software.TagTableGroup);
            }
            catch (Exception ex)
            {
                throw DecorateTagFailure(ex, softwarePath, string.Empty, "GetTagTables");
            }
        }

        /// <summary>Lists what one tag table holds: its tags and its user constants.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="tablePath">Full path of the table, as GetTagTables prints it.</param>
        /// <returns>Tags first, then user constants.</returns>
        /// <exception cref="PortalException">
        /// No project is open, or the software path or the table path does not resolve.
        /// </exception>
        public IReadOnlyList<TagInfo> GetTags(string softwarePath, string tablePath)
        {
            _logger?.LogInformation("Reading the tags of {TablePath} in {SoftwarePath}...", tablePath, softwarePath);

            try
            {
                var software = RequireSoftware(softwarePath);
                var table = RequireTable(software, tablePath);

                return new TagTableReader(_logger).ReadEntries(table);
            }
            catch (Exception ex)
            {
                throw DecorateTagFailure(ex, softwarePath, tablePath, "GetTags");
            }
        }

        /// <summary>Creates a tag table, and the groups above it when they are missing.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="tablePath">Full path of the table, for example <c>Cell/IO</c>.</param>
        /// <param name="backupDirectory">Where the current tables are exported first. Required.</param>
        /// <returns>The table as the project holds it, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the software path does not resolve, or the path is empty.
        /// </exception>
        /// <remarks>
        /// A table that is already there is returned rather than refused. A table is a container,
        /// and a caller asking for one that exists has got what it wanted — which is also the
        /// idempotence the repository requires of every write.
        /// </remarks>
        public TagTableInfo CreateTagTable(string softwarePath, string tablePath, string backupDirectory)
        {
            _logger?.LogInformation("Creating tag table {TablePath} in {SoftwarePath}...", tablePath, softwarePath);

            try
            {
                var software = RequireSoftwareForWrite(softwarePath, backupDirectory);
                var table = new TagTableLocator().Ensure(software.TagTableGroup, tablePath);

                return new TagTableInfo(ProjectPath.Parse(tablePath).ToString(), table.Tags.Count, table.UserConstants.Count, table.IsDefault);
            }
            catch (Exception ex)
            {
                throw DecorateTagFailure(ex, softwarePath, tablePath, "CreateTagTable");
            }
        }

        /// <summary>Creates a tag in a tag table.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="definition">Where the tag goes, its data type and its logical address.</param>
        /// <param name="backupDirectory">Where the current tables are exported first. Required.</param>
        /// <returns>The tag as the project holds it, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, a path does not resolve, the name is taken by something different,
        /// or TIA Portal refused the type or the address.
        /// </exception>
        public TagInfo CreateTag(string softwarePath, TagDefinition definition, string backupDirectory)
        {
            _logger?.LogInformation("Creating tag {Path} in {SoftwarePath}...", definition?.Path, softwarePath);

            try
            {
                var table = RequireTableForWrite(softwarePath, definition, backupDirectory);

                return new TagTableBuilder(_logger).CreateTag(table, definition!);
            }
            catch (Exception ex)
            {
                throw DecorateTagFailure(ex, softwarePath, definition?.Path ?? string.Empty, "CreateTag");
            }
        }

        /// <summary>Creates a user constant in a tag table.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="definition">Where the constant goes, its data type and its value.</param>
        /// <param name="backupDirectory">Where the current tables are exported first. Required.</param>
        /// <returns>The constant as the project holds it, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, a path does not resolve, the name is taken by something different,
        /// or TIA Portal refused the type or the value.
        /// </exception>
        public TagInfo CreateConstant(string softwarePath, TagDefinition definition, string backupDirectory)
        {
            _logger?.LogInformation("Creating constant {Path} in {SoftwarePath}...", definition?.Path, softwarePath);

            try
            {
                var table = RequireTableForWrite(softwarePath, definition, backupDirectory);

                return new TagTableBuilder(_logger).CreateConstant(table, definition!);
            }
            catch (Exception ex)
            {
                throw DecorateTagFailure(ex, softwarePath, definition?.Path ?? string.Empty, "CreateConstant");
            }
        }

        /// <summary>
        /// The PLC program a path names, refusing rather than returning nothing.
        /// </summary>
        /// <remarks>
        /// Shared with the OPC UA tools, which had their own copy of it until tag tables needed the
        /// same three checks. One partial class, one lookup.
        /// </remarks>
        private PlcSoftware RequireSoftware(string softwarePath)
        {
            if (string.IsNullOrWhiteSpace(softwarePath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "softwarePath is required");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before working with a PLC program");
            }

            return FindPlcSoftware(softwarePath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"PLC software not found: {softwarePath}");
        }

        /// <remarks>
        /// Names the tables that do exist. Sending somebody back into TIA Portal to look up a name
        /// this server is already holding is the failure GetSubnets was taught not to repeat.
        /// </remarks>
        private PlcTagTable RequireTable(PlcSoftware software, string tablePath)
        {
            var table = new TagTableLocator().Find(software.TagTableGroup, tablePath);
            if (table != null)
            {
                return table;
            }

            var present = new TagTableReader(_logger).ReadTables(software.TagTableGroup);

            throw new PortalException(
                PortalErrorCode.NotFound,
                $"No tag table at '{tablePath}'. The program has: {string.Join(", ", Paths(present))}");
        }

        /// <summary>The software a tag write targets, with its tables exported before anything changes.</summary>
        private PlcSoftware RequireSoftwareForWrite(string softwarePath, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes the program's names");
            }

            var software = RequireSoftware(softwarePath);

            new TagTableExporter(_logger).ExportAll(software.TagTableGroup, backupDirectory);

            return software;
        }

        /// <remarks>
        /// The table has to exist. Creating one implicitly here would mean a misspelt table path
        /// silently produced a second table holding one tag, and the program would compile.
        /// </remarks>
        private PlcTagTable RequireTableForWrite(string softwarePath, TagDefinition? definition, string backupDirectory)
        {
            if (definition == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "A definition is required");
            }

            var software = RequireSoftwareForWrite(softwarePath, backupDirectory);

            return RequireTable(software, definition.TablePath);
        }

        private static IEnumerable<string> Paths(IEnumerable<TagTableInfo> tables)
        {
            foreach (var table in tables)
            {
                yield return table.Path;
            }
        }

        private PortalException DecorateTagFailure(Exception ex, string softwarePath, string path, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["softwarePath"] = softwarePath;
            pex.Data["path"] = path;

            _logger?.LogError(pex, "{Operation} failed for {SoftwarePath} {Path}", operation, softwarePath, path);

            return pex;
        }
    }
}
