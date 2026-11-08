using Siemens.Engineering.SW.Tags;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Finds a tag table by path, and creates the groups along the way when asked to.
    /// </summary>
    /// <remarks>
    /// Tag tables nest in groups exactly as blocks do, so they are addressed the same way:
    /// <c>Cell/IO</c> is the table <c>IO</c> inside the group <c>Cell</c>. The reading of the path
    /// is <see cref="ProjectPath"/>'s, not a fourth one of its own — that this class does no string
    /// splitting is the point of it.
    ///
    /// <see cref="Ensure"/> creates the groups it walks through. That is not convenience: a caller
    /// asked to create <c>Cell/IO/Start</c> cannot be expected to first discover that <c>Cell</c>
    /// does not exist, create it, and try again, and the alternative — refusing — makes building a
    /// program from nothing a sequence of failures.
    /// </remarks>
    public sealed class TagTableLocator
    {
        /// <summary>Finds a tag table, without creating anything.</summary>
        /// <param name="root">The program's tag table group.</param>
        /// <param name="tablePath">Full path of the table, for example <c>Cell/IO</c>.</param>
        /// <returns>The table, or null when the path names no table that exists.</returns>
        /// <exception cref="PortalException">The path is empty.</exception>
        public PlcTagTable? Find(PlcTagTableGroup root, string tablePath)
        {
            var path = ProjectPath.Parse(tablePath);

            return FindGroup(root, path.Parent)?.TagTables.Find(path.Name);
        }

        /// <summary>Finds a tag table group, without creating anything.</summary>
        /// <param name="root">The program's tag table group.</param>
        /// <param name="groupPath">The group's path, e.g. <c>Cell/IO</c>; empty for the root.</param>
        /// <returns>The group, or null when part of the path is missing.</returns>
        /// <remarks>Names are matched exactly, through Openness's own <c>Find</c>, as every tag tool here matches them.</remarks>
        public PlcTagTableGroup? FindGroup(PlcTagTableGroup root, string groupPath)
        {
            PlcTagTableGroup? group = root;

            foreach (var name in ProjectPath.GroupSegments(groupPath))
            {
                group = group?.Groups.Find(name);
            }

            return group;
        }

        /// <summary>Finds a tag table, creating it and the groups above it if they are missing.</summary>
        /// <param name="root">The program's tag table group.</param>
        /// <param name="tablePath">Full path of the table, for example <c>Cell/IO</c>.</param>
        /// <returns>The table, existing or new.</returns>
        /// <exception cref="PortalException">The path is empty.</exception>
        /// <remarks>
        /// Returning an existing table rather than failing is what makes creating one idempotent,
        /// which the repository requires of every write. A table is a container: asking for one
        /// that is already there has got the caller exactly what it wanted.
        /// </remarks>
        public PlcTagTable Ensure(PlcTagTableGroup root, string tablePath)
        {
            var path = ProjectPath.Parse(tablePath);
            var group = EnsureGroups(root, path);

            return group.TagTables.Find(path.Name) ?? group.TagTables.Create(path.Name);
        }

        /// <summary>The name of every tag table in a program, in every group.</summary>
        /// <param name="root">The program's tag table group.</param>
        /// <returns>The names; TIA Portal keeps them unique across the program.</returns>
        public IEnumerable<string> AllNames(PlcTagTableGroup root)
        {
            foreach (PlcTagTable table in root.TagTables)
            {
                yield return table.Name;
            }

            foreach (PlcTagTableUserGroup group in root.Groups)
            {
                foreach (var name in AllNames(group))
                {
                    yield return name;
                }
            }
        }

        private static PlcTagTableGroup EnsureGroups(PlcTagTableGroup root, ProjectPath path)
        {
            var group = root;

            foreach (var name in GroupNames(path))
            {
                group = group.Groups.Find(name) ?? group.Groups.Create(name);
            }

            return group;
        }

        /// <summary>Every segment above the object itself: the groups to walk through.</summary>
        private static IEnumerable<string> GroupNames(ProjectPath path)
        {
            for (var index = 0; index < path.Segments.Count - 1; index++)
            {
                yield return path.Segments[index];
            }
        }
    }
}
