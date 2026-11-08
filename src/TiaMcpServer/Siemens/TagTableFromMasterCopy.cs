using Siemens.Engineering.Library.MasterCopies;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <summary>Creates a tag table from a master copy, and takes it back when TIA Portal renamed any of its tags.</summary>
    /// <remarks>
    /// Measured 2026-11-08 on TIA Portal V20: a master copy's content description names the table, never its tags, and
    /// <c>CreateFrom</c> with <c>ThrowIfExists</c> creates the table even when the program already holds one of its tags,
    /// renaming the new tag without a word (<c>MC_Running</c> became <c>MC_Running(1)</c>). So the program's tag and
    /// constant names are read before, the new table's after, and a table with a renamed name is deleted again.
    /// </remarks>
    internal static class TagTableFromMasterCopy
    {
        /// <summary>Creates the table, or refuses having created nothing.</summary>
        /// <param name="root">The program's tag table group, to read every name it already holds.</param>
        /// <param name="group">The group to create the table in.</param>
        /// <param name="copy">The master copy.</param>
        /// <returns>The new table's name.</returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> naming the tags and constants TIA Portal renamed, after the table is deleted.
        /// </exception>
        public static string Create(PlcTagTableGroup root, PlcTagTableGroup group, MasterCopy copy)
        {
            var before = Names(root).ToList();
            var table = group.TagTables.CreateFrom(copy, MasterCopyMode.ThrowIfExists);

            var renamed = MasterCopyRules.RenamedOnCreate(NamesIn(table), before);
            if (renamed.Count == 0)
            {
                return table.Name;
            }

            DeleteAgain(table);

            throw new PortalException(
                PortalErrorCode.InvalidState,
                $"The program already holds {MasterCopyRules.List(renamed.Select(name => $"'{name.Original}'"))}, which the tag table '{copy.Name}' brings; "
                + "TIA Portal renamed them without a word, so the table was deleted again and nothing was created. Rename or remove those tags first.");
        }

        /// <remarks>A failed undo names what it left behind: the message below would otherwise say nothing was created.</remarks>
        private static void DeleteAgain(PlcTagTable table)
        {
            var name = table.Name;

            try
            {
                table.Delete();
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal renamed tags of the new tag table '{name}', and deleting that table failed ({failure.Message.Trim()}); delete it in TIA Portal.", null, failure);
            }
        }

        private static IEnumerable<string> Names(PlcTagTableGroup group)
        {
            foreach (PlcTagTable table in group.TagTables)
            {
                foreach (var name in NamesIn(table))
                {
                    yield return name;
                }
            }

            foreach (PlcTagTableUserGroup nested in group.Groups)
            {
                foreach (var name in Names(nested))
                {
                    yield return name;
                }
            }
        }

        private static IEnumerable<string> NamesIn(PlcTagTable table)
        {
            return table.Tags.Select(tag => tag.Name).Concat(table.UserConstants.Select(constant => constant.Name));
        }
    }
}
