using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// An append-only audit trail, one JSON object per line.
    /// </summary>
    /// <remarks>
    /// JSON Lines rather than a database, for now. It needs no dependency, it survives a process
    /// dying mid-write with at most one truncated line, and it can be read with any text editor —
    /// which for a file whose whole job is to be trusted is worth more than query support. Phase 3
    /// swaps this for SQLite behind <see cref="IAuditTrail"/> when the gate needs real queries.
    ///
    /// Append-only is enforced by how it is written, not by a promise: the file is opened for
    /// append and never rewritten. Nothing here can edit history.
    /// </remarks>
    public sealed class JsonlAuditTrail : IAuditTrail
    {
        private readonly string _path;

        /// <summary>Creates a trail backed by a file.</summary>
        /// <param name="path">Where to write. Its directory is created if missing.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
        public JsonlAuditTrail(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("An audit trail needs a path", nameof(path));
            }

            _path = path;
        }

        /// <inheritdoc />
        public void Append(AuditEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            try
            {
                EnsureDirectory();

                // UTF-8 without a BOM: a BOM per append would land in the middle of the file.
                File.AppendAllText(_path, Serialise(entry) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                // Never swallowed. The caller decides what a failed write means — in Workshop Mode
                // it refuses the action outright, which is only possible if this throws.
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The audit trail at '{_path}' could not be written: {exception.Message}",
                    null,
                    exception);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<AuditEntry> Read()
        {
            if (!File.Exists(_path))
            {
                return Array.Empty<AuditEntry>();
            }

            var entries = new List<AuditEntry>();

            foreach (var line in File.ReadAllLines(_path))
            {
                var entry = Deserialise(line);

                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        private void EnsureDirectory()
        {
            var directory = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string Serialise(AuditEntry entry)
        {
            var record = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["timestamp"] = entry.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                ["planId"] = entry.PlanId.Value,
                ["mode"] = entry.Mode.ToString(),
                ["tool"] = entry.Tool,
                ["target"] = entry.Target,
                ["value"] = entry.Value,
                ["backupPath"] = entry.BackupPath,
                ["origin"] = entry.Origin,
                ["outcome"] = entry.Outcome.ToString(),
                ["detail"] = entry.Detail
            };

            return JsonSerializer.Serialize(record);
        }

        private static AuditEntry? Deserialise(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var record = JsonSerializer.Deserialize<Dictionary<string, string>>(line);

            if (record == null)
            {
                return null;
            }

            var request = new ChangeRequest(Field(record, "tool"), Field(record, "target"), Field(record, "value"), Field(record, "origin"))
                .WithBackup(Field(record, "backupPath"));
            var plan = new ChangePlan(
                PlanId.Parse(Field(record, "planId")),
                request,
                (OperationMode)Enum.Parse(typeof(OperationMode), Field(record, "mode")),
                DateTimeOffset.MinValue);

            return new AuditEntry(
                DateTimeOffset.Parse(Field(record, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                plan,
                (AuditOutcome)Enum.Parse(typeof(AuditOutcome), Field(record, "outcome")),
                Field(record, "detail"));
        }

        private static string Field(Dictionary<string, string> record, string name)
        {
            return record.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;
        }
    }
}
