using Siemens.Engineering.SW;
using Siemens.Engineering.SW.WatchAndForceTables;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the watch and force tables of a PLC program, and the rows of one of them.
    /// </summary>
    /// <remarks>
    /// Tables can sit in user groups, so the walk recurses for the reason every walk in this
    /// server does: a reader that stopped at the top would report that a program has no tables
    /// while its tables are one folder down.
    ///
    /// Comment rows are skipped. A watch table can hold them and they are real rows in TIA Portal,
    /// but Openness exposes nothing on one except <c>Delete</c> -- no comment, no address, nothing
    /// to print. They are still counted in a table's row count, which is why
    /// <see cref="WatchTableInfo.EntryCount"/> says rows rather than addresses.
    /// </remarks>
    public static class WatchTableReader
    {
        /// <summary>Reads every watch and force table of a PLC program.</summary>
        /// <param name="software">The PLC program to read.</param>
        /// <returns>The tables, the ones in user groups named by their group path.</returns>
        /// <exception cref="PortalException">No software was given.</exception>
        public static IReadOnlyList<WatchTableInfo> ReadTables(PlcSoftware software)
        {
            if (software == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "software is required");
            }

            var root = software.WatchAndForceTableGroup;
            var tables = new List<WatchTableInfo>();

            CollectTables(root.WatchTables, root.ForceTables, string.Empty, tables);

            foreach (var group in root.Groups)
            {
                CollectGroup(group, group.Name, tables);
            }

            return tables;
        }

        /// <summary>Finds one watch table by its path.</summary>
        /// <param name="software">The PLC program to search.</param>
        /// <param name="tablePath">The table, <c>Group/Name</c> or just <c>Name</c> at the root.</param>
        /// <returns>The table, or null when there is none of that name.</returns>
        public static PlcWatchTable? FindWatchTable(PlcSoftware software, string tablePath)
        {
            var target = ProjectPath.Parse(tablePath);

            return FindGroup(software, target.Parent) is { } group ? WatchTablesOf(group).Find(target.Name) : null;
        }

        /// <summary>Finds one force table by its path.</summary>
        /// <param name="software">The PLC program to search.</param>
        /// <param name="tablePath">The table, <c>Group/Name</c> or just <c>Name</c> at the root.</param>
        /// <returns>The table, or null when there is none of that name.</returns>
        public static PlcForceTable? FindForceTable(PlcSoftware software, string tablePath)
        {
            var target = ProjectPath.Parse(tablePath);

            return FindGroup(software, target.Parent) is { } group ? ForceTablesOf(group).Find(target.Name) : null;
        }

        /// <summary>Reads the rows of a watch table.</summary>
        /// <param name="table">The table to read.</param>
        /// <returns>One row per entry that has an address, in the order the table holds them.</returns>
        public static IReadOnlyList<WatchEntryInfo> ReadEntries(PlcWatchTable table)
        {
            var rows = new List<WatchEntryInfo>();

            foreach (var entry in table.Entries)
            {
                if (entry is PlcWatchTableEntry watched)
                {
                    rows.Add(Describe(watched));
                }
            }

            return rows;
        }

        /// <summary>Reads the rows of the force table.</summary>
        /// <param name="table">The table to read.</param>
        /// <returns>One row per entry that has an address, in the order the table holds them.</returns>
        public static IReadOnlyList<WatchEntryInfo> ReadEntries(PlcForceTable table)
        {
            var rows = new List<WatchEntryInfo>();

            foreach (var entry in table.Entries)
            {
                if (entry is PlcForceTableEntry forced)
                {
                    rows.Add(Describe(forced));
                }
            }

            return rows;
        }

        /// <summary>The watch tables held directly by a group.</summary>
        /// <param name="group">The system group or a user group.</param>
        /// <returns>Its watch tables.</returns>
        /// <remarks>
        /// The two group types carry the same three compositions and share no interface that
        /// declares them, so reaching them is a switch on the type rather than a property access.
        /// It is written once here instead of at each of the four call sites.
        /// </remarks>
        internal static PlcWatchTableComposition WatchTablesOf(PlcWatchAndForceTableGroup group)
        {
            return group switch
            {
                PlcWatchAndForceTableSystemGroup system => system.WatchTables,
                PlcWatchAndForceTableUserGroup user => user.WatchTables,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised watch table group: {group?.GetType().Name}")
            };
        }

        /// <summary>The force tables held directly by a group.</summary>
        /// <param name="group">The system group or a user group.</param>
        /// <returns>Its force tables.</returns>
        internal static PlcForceTableComposition ForceTablesOf(PlcWatchAndForceTableGroup group)
        {
            return group switch
            {
                PlcWatchAndForceTableSystemGroup system => system.ForceTables,
                PlcWatchAndForceTableUserGroup user => user.ForceTables,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised watch table group: {group?.GetType().Name}")
            };
        }

        private static WatchEntryInfo Describe(PlcWatchTableEntry entry)
        {
            var intention = entry.ModifyIntention
                ? new WatchEntryIntention(true, entry.ModifyValue ?? string.Empty, entry.ModifyTrigger.ToString())
                : WatchEntryIntention.None;

            return new WatchEntryInfo(
                entry.Address ?? string.Empty,
                entry.DisplayFormat.ToString(),
                entry.MonitorTrigger.ToString(),
                intention);
        }

        private static WatchEntryInfo Describe(PlcForceTableEntry entry)
        {
            // A force carries no trigger: it holds for as long as it is active, which is what makes
            // it different in kind from a modify rather than a louder version of one.
            var intention = entry.ForceIntention
                ? new WatchEntryIntention(true, entry.ForceValue ?? string.Empty, string.Empty)
                : WatchEntryIntention.None;

            return new WatchEntryInfo(
                entry.Address ?? string.Empty,
                entry.DisplayFormat.ToString(),
                entry.MonitorTrigger.ToString(),
                intention);
        }

        private static void CollectGroup(PlcWatchAndForceTableUserGroup group, string path, List<WatchTableInfo> tables)
        {
            CollectTables(group.WatchTables, group.ForceTables, path, tables);

            foreach (var child in group.Groups)
            {
                CollectGroup(child, ProjectPath.Join(path, child.Name), tables);
            }
        }

        private static void CollectTables(
            PlcWatchTableComposition watchTables,
            PlcForceTableComposition forceTables,
            string path,
            List<WatchTableInfo> tables)
        {
            foreach (var table in watchTables)
            {
                tables.Add(new WatchTableInfo(
                    ProjectPath.Join(path, table.Name),
                    WatchTableInfo.WatchKind,
                    table.Entries.Count,
                    table.IsConsistent));
            }

            foreach (var table in forceTables)
            {
                tables.Add(new WatchTableInfo(
                    ProjectPath.Join(path, table.Name),
                    WatchTableInfo.ForceKind,
                    table.Entries.Count,
                    table.IsConsistent));
            }
        }

        private static PlcWatchAndForceTableGroup? FindGroup(PlcSoftware software, string groupPath)
        {
            PlcWatchAndForceTableGroup? group = software.WatchAndForceTableGroup;

            foreach (var segment in ProjectPath.GroupSegments(groupPath))
            {
                group = group == null ? null : FindUserGroup(group, segment);
            }

            return group;
        }

        /// <summary>The user group of that name held directly by a group, or null.</summary>
        /// <param name="group">The system group or a user group to look in.</param>
        /// <param name="name">The name of the group to find.</param>
        /// <returns>The group, or null when there is none of that name.</returns>
        internal static PlcWatchAndForceTableUserGroup? FindUserGroup(PlcWatchAndForceTableGroup group, string name)
        {
            return group switch
            {
                PlcWatchAndForceTableSystemGroup system => system.Groups.Find(name),
                PlcWatchAndForceTableUserGroup user => user.Groups.Find(name),
                _ => null
            };
        }
    }
}
