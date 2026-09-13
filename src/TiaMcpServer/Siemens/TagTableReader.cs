using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW.Tags;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads a program's tag tables: which there are, and what is in one.
    /// </summary>
    /// <remarks>
    /// The read the writes are aimed with. A tag's path is its table's path plus its name, so
    /// listing tables by full path is what makes <c>CreateTag</c> callable at all — the same
    /// property the network tools have, that what one tool prints another tool takes.
    ///
    /// Tags and user constants are read together because they are one table in TIA Portal's own
    /// editor and one namespace to SCL. System constants are not: they are generated from the
    /// hardware configuration, nothing can author them, and listing them among things that can be
    /// created would suggest otherwise.
    /// </remarks>
    public sealed class TagTableReader
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a tag table reader.</summary>
        /// <param name="logger">Optional logger.</param>
        public TagTableReader(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Lists every tag table of a program, by full path.</summary>
        /// <param name="root">The program's tag table group.</param>
        /// <returns>One entry per table, groups walked depth first.</returns>
        /// <exception cref="PortalException">No group was given.</exception>
        public IReadOnlyList<TagTableInfo> ReadTables(PlcTagTableGroup root)
        {
            if (root == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The tag table group is required");
            }

            var tables = new List<TagTableInfo>();

            Collect(root, string.Empty, tables);

            _logger?.LogInformation("Tag tables: {Count} found", tables.Count);

            return tables;
        }

        /// <summary>Lists what one table holds: its tags and its user constants.</summary>
        /// <param name="table">The table to read.</param>
        /// <returns>Tags first, then user constants.</returns>
        /// <exception cref="PortalException">No table was given.</exception>
        public IReadOnlyList<TagInfo> ReadEntries(PlcTagTable table)
        {
            if (table == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The tag table is required");
            }

            var tags = table.Tags.Select(Describe);
            var constants = table.UserConstants.Select(Describe);

            return tags.Concat(constants).ToList();
        }

        /// <summary>Describes one tag as it stands in the project.</summary>
        /// <param name="tag">The tag.</param>
        /// <returns>Its name, type and address.</returns>
        public static TagInfo Describe(PlcTag tag)
        {
            return new TagInfo(tag.Name, TagInfo.TagKind, tag.DataTypeName, tag.LogicalAddress ?? string.Empty);
        }

        /// <summary>Describes one user constant as it stands in the project.</summary>
        /// <param name="constant">The constant.</param>
        /// <returns>Its name, type and value.</returns>
        public static TagInfo Describe(PlcUserConstant constant)
        {
            return new TagInfo(
                constant.Name,
                TagInfo.ConstantKind,
                constant.DataTypeName,
                constant.Value?.ToString() ?? string.Empty);
        }

        private static void Collect(PlcTagTableGroup group, string groupPath, List<TagTableInfo> tables)
        {
            foreach (var table in group.TagTables)
            {
                tables.Add(new TagTableInfo(
                    ProjectPath.Join(groupPath, table.Name),
                    table.Tags.Count,
                    table.UserConstants.Count,
                    table.IsDefault));
            }

            foreach (var nested in group.Groups)
            {
                Collect(nested, ProjectPath.Join(groupPath, nested.Name), tables);
            }
        }
    }
}
