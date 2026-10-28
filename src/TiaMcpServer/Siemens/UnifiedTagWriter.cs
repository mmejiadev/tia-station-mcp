using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.HmiTags;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedTags;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Creates WinCC Unified HMI tags, all of a list or none of it.</summary>
    /// <remarks>
    /// Measured 2026-10-22 on TIA Portal V20. A tag links to a PLC tag only through an integrated
    /// connection — one with a partner PLC — that already exists: Openness cannot create one (its
    /// <c>Partner</c> is read-only), and through a driver connection <c>PlcTag</c> finds nothing. Setting
    /// <c>Connection</c> and then <c>PlcTag</c> on a new tag is enough; TIA Portal fills in the PLC, the
    /// data type and the address, and refuses a PLC tag it does not find. <c>Connection</c> accepts the
    /// name of a connection that does not exist, silently, so every connection is checked here first and
    /// written with the connection's own spelling, never the caller's. A tag is never read through
    /// <c>GetAttributeInfos()</c>, which ends the session on a new tag.
    /// Measured 2026-10-25: table names are unique on the whole HMI, groups or not, and group names in any
    /// case; deleting a group deletes its tables and their tags; a tag can be named like a system tag, and
    /// a limit can name a tag that does not exist, both silently, so both are refused here. A limit tag of
    /// another data type than the tag it limits compiles as "Only HMI tags of the same data type are
    /// allowed"; a linked tag's type is known only once TIA Portal has filled it in, so that is checked
    /// once every tag exists, and a mismatch undoes the call.
    /// </remarks>
    internal sealed class UnifiedTagWriter
    {
        private readonly UnifiedHmiSoftware _hmi;
        private readonly UnifiedTagSettingsWriter _settings;

        /// <summary>Creates a writer for one HMI.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="language">The project's editing language, the one comments are written in.</param>
        public UnifiedTagWriter(UnifiedHmiSoftware hmi, Language language)
        {
            _hmi = hmi;
            _settings = new UnifiedTagSettingsWriter(language);
        }

        /// <summary>Refuses a list that cannot be created as it stands, before anything is changed.</summary>
        /// <param name="list">The tables and tags.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming every problem found.</exception>
        public void Check(UnifiedTagList list)
        {
            var newTables = list.NewTables.Select(UnifiedTagListReader.TableName).ToList();
            var problems = newTables.Where(table => UnifiedTagReader.FindTable(_hmi, table) != null).Select(table => $"tag table '{table}' already exists (table names are unique on the whole HMI)").ToList();

            problems.AddRange(list.Tags.Where(tag => tag.Table != null).Select(tag => TableProblem(tag, list.NewTables)).OfType<string>());

            problems.AddRange(list.Tags.Where(tag => _hmi.Tags.Find(tag.Name) != null).Select(tag => $"a tag named '{tag.Name}' already exists"));
            problems.AddRange(list.Tags.Where(tag => _hmi.SystemTags.Find(tag.Name) != null).Select(tag => $"'{tag.Name}' is the name of a system tag"));
            problems.AddRange(list.Tags.Where(tag => tag.Link != null).Select(ConnectionProblem).OfType<string>());
            problems.AddRange(MissingLimitTags(list));

            if (problems.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was created: {string.Join("; ", problems)}");
            }
        }

        /// <summary>Creates the groups and tables, then the tags; undoes all of it when one fails.</summary>
        /// <param name="list">The tables and tags, already passed through <see cref="Check"/>.</param>
        /// <returns>Each tag created, with the path of the table it is in, in the list's order.</returns>
        public IReadOnlyList<(HmiTag Tag, string Table)> Create(UnifiedTagList list)
        {
            var made = new Made();

            try
            {
                foreach (var path in list.NewTables)
                {
                    made.Tables.Add(CreateTable(path, made.Groups));
                }

                foreach (var tag in list.Tags)
                {
                    made.Tags.Add(CreateTag(tag));
                }

                RequireLimitTypes(list);

                return made.Tags;
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw Undo(made, failure);
            }
        }

        /// <summary>What one call has created so far, so a failure can remove it again.</summary>
        private sealed class Made
        {
            public List<(HmiTag Tag, string Table)> Tags { get; } = new List<(HmiTag Tag, string Table)>();

            public List<HmiTagTable> Tables { get; } = new List<HmiTagTable>();

            /// <summary>Only the outermost new group of each path: deleting it deletes what is inside.</summary>
            public List<HmiTagTableGroup> Groups { get; } = new List<HmiTagTableGroup>();
        }

        /// <remarks>
        /// A table is found by its name, which is unique on the HMI; a path given with it must be that
        /// table's path, so a tag never lands in a table other than the one written.
        /// </remarks>
        private string? TableProblem(UnifiedTagDescription tag, IReadOnlyList<string> newPaths)
        {
            var name = UnifiedTagListReader.TableName(tag.Table!);
            var path = UnifiedTagReader.FindTable(_hmi, name)?.Path ?? newPaths.FirstOrDefault(candidate => UnifiedTagListReader.TableName(candidate).Equals(name, StringComparison.OrdinalIgnoreCase));

            if (path == null)
            {
                return $"'{tag.Name}' goes in tag table '{tag.Table}', which does not exist; list it under 'newTables' to create it";
            }

            var written = tag.Table!;

            return written.Contains('/') && !written.Equals(path, StringComparison.OrdinalIgnoreCase)
                ? $"'{tag.Name}' goes in tag table '{tag.Table}', but that table is '{path}'"
                : null;
        }

        private IEnumerable<string> MissingLimitTags(UnifiedTagList list)
        {
            var listed = new HashSet<string>(list.Tags.Select(tag => tag.Name), StringComparer.OrdinalIgnoreCase);

            return list.Tags
                .SelectMany(tag => tag.Settings.Limits.TagNames.Select(holder => (Tag: tag.Name, Holder: holder)))
                .Where(entry => !listed.Contains(entry.Holder) && _hmi.Tags.Find(entry.Holder) == null)
                .Select(entry => $"'{entry.Tag}' takes a limit from tag '{entry.Holder}', which does not exist");
        }

        private void RequireLimitTypes(UnifiedTagList list)
        {
            var mismatches = list.Tags
                .SelectMany(tag => tag.Settings.Limits.TagNames.Select(holder => (Tag: _hmi.Tags.Find(tag.Name), Holder: _hmi.Tags.Find(holder))))
                .Where(entry => entry.Tag.DataType != entry.Holder.DataType)
                .Select(entry => $"'{entry.Tag.Name}' ({entry.Tag.DataType}) takes a limit from '{entry.Holder.Name}' ({entry.Holder.DataType}); a limit tag must be of the same data type")
                .ToList();

            if (mismatches.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, string.Join("; ", mismatches));
            }
        }

        private HmiTagTable CreateTable(string path, List<HmiTagTableGroup> newGroups)
        {
            var tables = _hmi.TagTables;
            HmiTagTableGroupComposition groups = _hmi.TagTableGroups;

            foreach (var name in UnifiedTagListReader.GroupsOf(path))
            {
                var group = groups.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    group = groups.Create(name);
                    AddUnlessInsideNew(newGroups, group);
                }

                tables = group.TagTables;
                groups = group.Groups;
            }

            return tables.Create(UnifiedTagListReader.TableName(path));
        }

        /// <remarks>A group inside one this call created goes when that one is deleted, and deleting it twice would fail.</remarks>
        private static void AddUnlessInsideNew(List<HmiTagTableGroup> newGroups, HmiTagTableGroup group)
        {
            if (!(group.Parent is HmiTagTableGroup parent && newGroups.Any(created => created.Equals(parent) || IsInside(parent, created))))
            {
                newGroups.Add(group);
            }
        }

        private static bool IsInside(HmiTagTableGroup group, HmiTagTableGroup ancestor)
        {
            return group.Parent is HmiTagTableGroup parent && (parent.Equals(ancestor) || IsInside(parent, ancestor));
        }

        /// <remarks>
        /// A name TIA Portal refuses — longer than 128 characters — is a bad request, reported as such; a
        /// value that does not fit a linked tag's type keeps the refusal it was given.
        /// </remarks>
        private (HmiTag Tag, string Table) CreateTag(UnifiedTagDescription description)
        {
            var table = description.Table == null ? null : UnifiedTagReader.FindTable(_hmi, UnifiedTagListReader.TableName(description.Table));
            HmiTag? tag = null;

            try
            {
                tag = table == null ? _hmi.Tags.Create(description.Name) : _hmi.Tags.Create(description.Name, table.Value.Table.Name);
                Configure(tag, description);
                _settings.Write(tag, description);

                return (tag, table?.Path ?? UnifiedTagReader.TableOf(_hmi, tag));
            }
            catch (PortalException refusal)
            {
                throw new PortalException(refusal.Code, refusal.Message + RemoveHalfMade(tag), null, refusal);
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused tag '{description.Name}': {failure.Message.Trim()}{RemoveHalfMade(tag)}", null, failure);
            }
        }

        /// <remarks>
        /// The tag being made is in no list Undo walks, so a delete that fails is named here; it must not
        /// replace the refusal that explains what went wrong.
        /// </remarks>
        private static string RemoveHalfMade(HmiTag? tag)
        {
            if (tag == null)
            {
                return string.Empty;
            }

            var name = tag.Name;

            return UnifiedRollback.TryDelete(tag.Delete) ? string.Empty : $" Removing the half-made tag '{name}' failed too; delete it in TIA Portal.";
        }

        private void Configure(HmiTag tag, UnifiedTagDescription description)
        {
            if (description.Link == null)
            {
                tag.DataType = description.DataType;
                return;
            }

            tag.Connection = FindConnection(description.Link.Connection)!.Name;
            tag.PlcTag = description.Link.PlcTag;
        }

        private global::Siemens.Engineering.HmiUnified.HmiConnections.HmiConnection? FindConnection(string name)
        {
            return _hmi.Connections.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <remarks>
        /// A connection without a partner is a driver connection, through which no PLC tag is found;
        /// the integrated connection is made once by hand in TIA Portal (Devices &amp; networks, Connections).
        /// </remarks>
        private string? ConnectionProblem(UnifiedTagDescription tag)
        {
            var connection = FindConnection(tag.Link!.Connection);

            if (connection == null)
            {
                return $"'{tag.Name}' names connection '{tag.Link.Connection}', which this HMI does not have (it has: {string.Join(", ", _hmi.Connections.Select(candidate => candidate.Name))})";
            }

            return string.IsNullOrEmpty(connection.Partner)
                ? $"'{tag.Name}': connection '{connection.Name}' has no partner PLC, so no PLC tag can be linked through it; make the integrated connection to the PLC in TIA Portal once"
                : null;
        }

        /// <remarks>Tags go first, then tables, then the new groups, which take with them anything still inside.</remarks>
        private static PortalException Undo(Made made, Exception failure)
        {
            var inOrder = made.Groups.Select(group => (Name: $"tag table group '{group.Name}'", Delete: (Action)group.Delete))
                .Concat(made.Tables.Select(table => (Name: $"tag table '{table.Name}'", Delete: (Action)table.Delete)))
                .Concat(made.Tags.Select(entry => (Name: $"tag '{entry.Tag.Name}'", Delete: (Action)entry.Tag.Delete)))
                .ToList();

            return UnifiedRollback.Undo(inOrder, failure, "tags, tables and groups");
        }
    }
}
