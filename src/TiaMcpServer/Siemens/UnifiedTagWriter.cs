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
    /// </remarks>
    internal sealed class UnifiedTagWriter
    {
        private readonly UnifiedHmiSoftware _hmi;

        /// <summary>Creates a writer for one HMI.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        public UnifiedTagWriter(UnifiedHmiSoftware hmi)
        {
            _hmi = hmi;
        }

        /// <summary>Refuses a list that cannot be created as it stands, before anything is changed.</summary>
        /// <param name="list">The tables and tags.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming every problem found.</exception>
        public void Check(UnifiedTagList list)
        {
            var problems = list.NewTables.Where(table => UnifiedTagReader.FindTable(_hmi, table) != null).Select(table => $"tag table '{table}' already exists").ToList();

            problems.AddRange(list.Tags
                .Where(tag => tag.Table != null && UnifiedTagReader.FindTable(_hmi, tag.Table) == null && !list.NewTables.Contains(tag.Table, StringComparer.OrdinalIgnoreCase))
                .Select(tag => $"'{tag.Name}' goes in tag table '{tag.Table}', which does not exist; list it under 'newTables' to create it"));

            problems.AddRange(list.Tags.Where(tag => _hmi.Tags.Find(tag.Name) != null).Select(tag => $"a tag named '{tag.Name}' already exists"));
            problems.AddRange(list.Tags.Where(tag => tag.Link != null).Select(ConnectionProblem).OfType<string>());

            if (problems.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was created: {string.Join("; ", problems)}");
            }
        }

        /// <summary>Creates the tables, then the tags; undoes all of it when one fails.</summary>
        /// <param name="list">The tables and tags, already passed through <see cref="Check"/>.</param>
        /// <returns>Each tag created, with the path of the table it is in, in the list's order.</returns>
        public IReadOnlyList<(HmiTag Tag, string Table)> Create(UnifiedTagList list)
        {
            var tables = new List<HmiTagTable>();
            var tags = new List<(HmiTag Tag, string Table)>();

            try
            {
                tables.AddRange(list.NewTables.Select(table => _hmi.TagTables.Create(table)));

                foreach (var tag in list.Tags)
                {
                    tags.Add(CreateTag(tag));
                }

                return tags;
            }
            catch (Exception failure) when (!IsSessionEnded(failure))
            {
                throw Undo(tags.Select(entry => entry.Tag).ToList(), tables, failure);
            }
        }

        /// <remarks>A name TIA Portal refuses — longer than 128 characters — is a bad request, reported as such.</remarks>
        private (HmiTag Tag, string Table) CreateTag(UnifiedTagDescription description)
        {
            var table = description.Table == null ? null : UnifiedTagReader.FindTable(_hmi, description.Table);
            HmiTag? tag = null;

            try
            {
                tag = table == null ? _hmi.Tags.Create(description.Name) : _hmi.Tags.Create(description.Name, table.Value.Table.Name);
                Configure(tag, description);

                return (tag, table?.Path ?? UnifiedTagReader.TableOf(_hmi, tag));
            }
            catch (Exception failure) when (!IsSessionEnded(failure))
            {
                tag?.Delete();
                throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused tag '{description.Name}': {failure.Message.Trim()}", null, failure);
            }
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

        /// <remarks>Once the session has ended there is nothing to undo with; the caller must reopen the project.</remarks>
        private static bool IsSessionEnded(Exception failure)
        {
            return failure is global::Siemens.Engineering.NonRecoverableException || failure is global::Siemens.Engineering.EngineeringObjectDisposedException;
        }

        /// <remarks>
        /// A delete that fails must not hide the failure that explains what went wrong, and the caller
        /// must learn what is left, so a retry does not stumble on it: compare RemoveStationAfter.
        /// </remarks>
        private static PortalException Undo(List<HmiTag> tags, List<HmiTagTable> tables, Exception failure)
        {
            var left = Enumerable.Reverse(tags).Select(tag => (Name: $"tag '{tag.Name}'", Delete: (Action)tag.Delete))
                .Concat(Enumerable.Reverse(tables).Select(table => (Name: $"tag table '{table.Name}'", Delete: (Action)table.Delete)))
                .Where(entry => !TryDelete(entry.Delete))
                .Select(entry => entry.Name)
                .ToList();

            var code = (failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed;
            var outcome = left.Count == 0
                ? "Nothing was created: the tags and tables made before it were removed again."
                : $"Removing what was made before it failed for {string.Join(", ", left)}; delete those in TIA Portal before trying again.";

            return new PortalException(code, $"{failure.Message.Trim()} {outcome}", null, failure);
        }

        private static bool TryDelete(Action delete)
        {
            try
            {
                delete();
                return true;
            }
            catch (Exception deleteFailure) when (!IsSessionEnded(deleteFailure))
            {
                // Not swallowed: the caller of Undo names every object whose delete failed.
                return false;
            }
        }
    }
}
