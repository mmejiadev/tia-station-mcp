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
    ///
    /// **Appends are serialised.** A write started as a job runs on the thread pool while the thread
    /// serving the protocol may be auditing something else, and two unsynchronised appends to one
    /// file can interleave or fail on a sharing violation. Losing an audit line is the one failure
    /// this class exists to make impossible, so the lock is not an optimisation to reconsider.
    ///
    /// The lock is per instance, which is enough because the server holds exactly one: it is
    /// registered as a singleton. It does **not** protect against a second process writing the same
    /// file, and nothing here pretends to.
    /// </remarks>
    public sealed class JsonlAuditTrail : IAuditTrail
    {
        /// <summary>The version of the canonical form this server writes.</summary>
        private const string CurrentChainVersion = "2";

        /// <summary>The version assumed for a line that names none.</summary>
        private const string OriginalChainVersion = "1";

        /// <summary>
        /// The order the chain hashes an entry's values in, per version of the canonical form.
        /// </summary>
        /// <remarks>
        /// **A list may never be reordered or edited once it has shipped.** The hash covers the
        /// values in this order and nothing else, so changing a published list makes every entry
        /// written under it report as edited after the fact -- which is the strongest alarm this
        /// system has, raised by a routine code change.
        ///
        /// Adding a field means adding a *version*, and this is why. Until 2026-09-05 the comment
        /// here promised that a field appended to the end would leave earlier entries verifiable.
        /// It would not have: the canonical form is a JSON array of values, so an eleventh value
        /// changes the hash of an entry written with ten. Measured against the golden fixture with
        /// the harness verifier rather than reasoned about, because the promise had been believed
        /// once already.
        ///
        /// A line records its version in <c>v</c>, and one written before versioning existed
        /// records none and is read as version 1. <c>v</c> is not itself hashed and does not need to
        /// be: editing it makes the entry verify against the wrong field list, and the hash stops
        /// matching. It fails closed.
        /// </remarks>
        private static readonly Dictionary<string, string[]> ChainedFieldsByVersion =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [OriginalChainVersion] = new[]
                {
                    "timestamp", "planId", "mode", "tool", "target", "value", "backupPath", "origin", "outcome", "detail"
                },
                [CurrentChainVersion] = new[]
                {
                    "timestamp", "planId", "mode", "tool", "target", "value", "backupPath", "origin", "outcome", "detail",
                    "documentation"
                }
            };

        /// <summary>Why a line with its chain fields removed is a forgery and not old history.</summary>
        private const string StrippedChain =
            "this entry carries no chain fields although the chain had already begun - they were stripped from it";

        private readonly object _gate = new object();
        private readonly string _path;

        private bool _chainLoaded;
        private long _lastSequence;
        private string _lastHash = AuditChain.Root;

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
                lock (_gate)
                {
                    EnsureDirectory();
                    LoadChainState();

                    var sequence = _lastSequence + 1;
                    var line = Serialise(entry, sequence, _lastHash);

                    // UTF-8 without a BOM: a BOM per append would land in the middle of the file.
                    File.AppendAllText(_path, line.Text + Environment.NewLine, new UTF8Encoding(false));

                    // Only after the write. A hash remembered for a line that never reached disk
                    // would break the chain for every entry after it.
                    _lastSequence = sequence;
                    _lastHash = line.Hash;
                }
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

        /// <summary>Checks that every chained entry still matches the one before it.</summary>
        /// <returns>What the check found, including how much of the file predates chaining.</returns>
        /// <remarks>
        /// Read-only and side-effect free: it answers a question and repairs nothing. A method that
        /// "fixed" a broken chain would destroy the only evidence that something was edited.
        ///
        /// **Unattested history can only be a prefix of the file.** Chaining was switched on once,
        /// and everything written afterwards carries it, so a line with no chain fields *after* the
        /// chain has begun is one somebody removed the hash from — the cheapest forgery there is:
        /// edit an entry, delete its hash, and it reads as history from before the chain existed.
        ///
        /// Before 2026-09-02 this method skipped such a line wherever it sat. In the middle of a
        /// file that was caught anyway, but by accident and with the wrong diagnosis: the *next*
        /// entry was reported as removed or inserted, because its sequence no longer followed. On
        /// the **last** line nothing follows to give it away, and the trail reported intact. Both
        /// cases have a test.
        /// </remarks>
        public AuditChainReport VerifyChain()
        {
            if (!File.Exists(_path))
            {
                return new AuditChainReport(0, 0, 0, string.Empty);
            }

            var previousHash = AuditChain.Root;
            var expectedSequence = 0L;
            var chained = 0;
            var unchained = 0;
            var lineNumber = 0;

            foreach (var line in File.ReadLines(_path))
            {
                lineNumber++;

                var record = TryRead(line);

                if (record == null)
                {
                    continue;
                }

                if (!record.ContainsKey("hash") && expectedSequence > 0)
                {
                    return new AuditChainReport(chained, unchained, lineNumber, StrippedChain);
                }

                if (!record.ContainsKey("hash"))
                {
                    unchained++;

                    continue;
                }

                expectedSequence++;

                var failure = Break(record, expectedSequence, previousHash);

                if (failure.Length > 0)
                {
                    return new AuditChainReport(chained, unchained, lineNumber, failure);
                }

                chained++;
                previousHash = Field(record, "hash");
            }

            return new AuditChainReport(chained, unchained, 0, string.Empty);
        }

        /// <summary>Why this line does not follow the previous one, or empty when it does.</summary>
        /// <param name="record">The line's fields.</param>
        /// <param name="expectedSequence">The position this line has to claim.</param>
        /// <param name="previousHash">The hash the line has to point back to.</param>
        /// <returns>The reason, or an empty string.</returns>
        /// <remarks>
        /// Three ways to fail, and each names a different tampering. A wrong sequence means a line
        /// was removed or inserted; a wrong predecessor means the file was cut and re-joined; a hash
        /// that does not recompute means the entry's own values were edited.
        /// </remarks>
        private static string Break(Dictionary<string, string> record, long expectedSequence, string previousHash)
        {
            if (Field(record, "seq") != expectedSequence.ToString(CultureInfo.InvariantCulture))
            {
                return $"expected sequence {expectedSequence}, found '{Field(record, "seq")}' - an entry was removed or inserted";
            }

            if (Field(record, "prev") != previousHash)
            {
                return "this entry does not point back to the one before it - the file was cut and re-joined";
            }

            var version = Field(record, "v");

            if (version.Length == 0)
            {
                version = OriginalChainVersion;
            }

            if (!ChainedFieldsByVersion.TryGetValue(version, out var chainedFields))
            {
                return $"this entry names chain version '{version}', which this server does not know - " +
                    "it was written by a newer one";
            }

            var values = new List<string>(chainedFields.Length);

            foreach (var name in chainedFields)
            {
                values.Add(Field(record, name));
            }

            if (AuditChain.Link(expectedSequence, previousHash, values) != Field(record, "hash"))
            {
                return "the entry's own values do not match its hash - it was edited after it was written";
            }

            return string.Empty;
        }

        private void EnsureDirectory()
        {
            var directory = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>One serialised line and the hash that links it to the entry before it.</summary>
        private struct ChainedLine
        {
            public string Text;
            public string Hash;
        }

        private static ChainedLine Serialise(AuditEntry entry, long sequence, string previousHash)
        {
            var record = Fields(entry);
            var chainedFields = ChainedFieldsByVersion[CurrentChainVersion];
            var values = new List<string>(chainedFields.Length);

            foreach (var name in chainedFields)
            {
                values.Add(record[name]);
            }

            var hash = AuditChain.Link(sequence, previousHash, values);

            record["v"] = CurrentChainVersion;
            record["seq"] = sequence.ToString(CultureInfo.InvariantCulture);
            record["prev"] = previousHash;
            record["hash"] = hash;

            return new ChainedLine { Text = JsonSerializer.Serialize(record), Hash = hash };
        }

        private static Dictionary<string, string> Fields(AuditEntry entry)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
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
                ["detail"] = entry.Detail,
                ["documentation"] = entry.Documentation
            };
        }

        /// <summary>
        /// Finds where the chain has got to, once per process.
        /// </summary>
        /// <remarks>
        /// From the last line that carries chain fields, so appending costs one pass over the file
        /// on the first write and nothing afterwards. A trail with no chained line at all starts one
        /// at sequence 1: chaining was added to a file that already held history, and that history
        /// is reported as unattested rather than rewritten to look verified.
        /// </remarks>
        private void LoadChainState()
        {
            if (_chainLoaded)
            {
                return;
            }

            _chainLoaded = true;

            if (!File.Exists(_path))
            {
                return;
            }

            foreach (var line in File.ReadLines(_path))
            {
                var record = TryRead(line);

                if (record != null && record.TryGetValue("hash", out var hash))
                {
                    _lastHash = hash;
                    _lastSequence = long.TryParse(Field(record, "seq"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq)
                        ? seq
                        : _lastSequence + 1;
                }
            }
        }

        private static Dictionary<string, string>? TryRead(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(line);
            }
            catch (JsonException)
            {
                // An unreadable line is not a reason to stop reading the file: the gate counts
                // unreadable lines as a criterion of its own, and it can only count what it reaches.
                return null;
            }
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
                Field(record, "detail"),
                Field(record, "documentation"));
        }

        private static string Field(Dictionary<string, string> record, string name)
        {
            return record.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;
        }
    }
}
