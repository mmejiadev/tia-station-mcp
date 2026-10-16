using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Hmi
{
    /// <summary>Chooses which PLC tags get an HMI tag.</summary>
    /// <remarks>
    /// Memory tags, because those are what a panel writes and reads in the class projects: a button
    /// sets <c>Marxa</c> (<c>%M</c>) and the program reads it, never the input the push button is
    /// wired to. Each HMI tag takes its PLC tag's name, as the teacher's tables do (<c>Marxa</c> →
    /// <c>Marxa</c>), so a screen can be written with the names of the program.
    ///
    /// HMI tag names are unique across all tables of a panel, so a name that already exists in
    /// another table is left out rather than duplicated — which also makes running it twice change
    /// nothing.
    /// </remarks>
    public static class HmiTagSelection
    {
        private const string MemoryAreaPrefix = "%M";

        /// <summary>Plans an HMI tag for every memory tag of a PLC program.</summary>
        /// <param name="plcTags">Every tag of the PLC program, from all its tables.</param>
        /// <param name="existingHmiTags">The panel's HMI tags: tag name to the table holding it.</param>
        /// <param name="targetTable">The HMI tag table the generated tags go into.</param>
        /// <returns>The plan. Check <see cref="HmiTagPlan.Displaced"/> before writing it.</returns>
        public static HmiTagPlan ForMemoryTags(
            IEnumerable<TagInfo> plcTags,
            IReadOnlyDictionary<string, string> existingHmiTags,
            string targetTable)
        {
            var memoryTags = plcTags.Where(IsMemoryTag).Select(tag => tag.Name).ToList();
            var inOtherTables = memoryTags
                .Where(name => existingHmiTags.TryGetValue(name, out var table) && !IsSameName(table, targetTable))
                .ToList();

            var links = memoryTags
                .Except(inOtherTables, StringComparer.OrdinalIgnoreCase)
                .Select(name => new HmiTagLink(name, name))
                .ToList();

            var generated = new HashSet<string>(links.Select(link => link.HmiTagName), StringComparer.OrdinalIgnoreCase);
            var displaced = existingHmiTags
                .Where(entry => IsSameName(entry.Value, targetTable) && !generated.Contains(entry.Key))
                .Select(entry => entry.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            return new HmiTagPlan(links, inOtherTables, displaced);
        }

        private static bool IsMemoryTag(TagInfo tag)
        {
            return tag.Kind == TagInfo.TagKind
                && (tag.Assignment ?? string.Empty).StartsWith(MemoryAreaPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameName(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
