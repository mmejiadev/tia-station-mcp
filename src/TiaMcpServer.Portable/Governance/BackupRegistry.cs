using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// One configured root, one timestamped directory per change, listable.
    /// </summary>
    /// <remarks>
    /// This replaced a mandatory <c>backupDirectory</c> parameter on the write tools. The parameter
    /// was not optional, so nothing could be skipped — but the caller chose the location, which
    /// meant nobody could enumerate what had been saved and an agent could point it at a temp
    /// directory Windows would reap. What a backup is for is being found later by a person who did
    /// not take it.
    ///
    /// A manifest is written into each directory at allocation, before the export runs. It records
    /// only what is true at that moment — which tool, which target, when — and never claims the
    /// change succeeded; the audit trail is what says that. So a directory with a manifest and no
    /// files is a write that was refused or that failed before exporting, and reads as exactly that
    /// in <see cref="List"/>.
    /// </remarks>
    public sealed class BackupRegistry : IBackupRegistry
    {
        private const string ManifestName = "backup.json";
        private const int MaxTargetLength = 60;

        private readonly string _root;
        private readonly ISystemClock _clock;

        /// <summary>Creates a registry over a root directory.</summary>
        /// <param name="root">Where every backup goes. Created on first use.</param>
        /// <param name="clock">Where the timestamps come from.</param>
        /// <exception cref="ArgumentException"><paramref name="root"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        public BackupRegistry(string root, ISystemClock clock)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentException("A backup registry needs a root directory", nameof(root));
            }

            _root = root;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="tool"/> or <paramref name="target"/> is empty.</exception>
        /// <exception cref="PortalException">The directory could not be created or the manifest written.</exception>
        public string Allocate(string tool, string target)
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                throw new ArgumentException("A backup must name the tool it is for", nameof(tool));
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("A backup must name what it is protecting", nameof(target));
            }

            var takenAt = _clock.UtcNow;
            var path = ReserveDirectory(takenAt, tool, target);

            WriteManifest(path, tool, target, takenAt);

            return path;
        }

        /// <inheritdoc />
        public IReadOnlyList<BackupRecord> List()
        {
            if (!Directory.Exists(_root))
            {
                return Array.Empty<BackupRecord>();
            }

            return Directory.GetDirectories(_root)
                .Select(ReadRecord)
                .Where(record => record != null)
                .Select(record => record!)
                .OrderByDescending(record => record.TakenAt)
                .ToList();
        }

        private string ReserveDirectory(DateTimeOffset takenAt, string tool, string target)
        {
            var stamp = takenAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var name = $"{stamp}-{Sanitise(tool)}-{Sanitise(target)}";

            try
            {
                // Two changes within the same second are rare and entirely legal, so the suffix is
                // not paranoia: without it the second one would export its previous state on top of
                // the first one's, and the backup that mattered would be the one that got mixed.
                var path = Path.Combine(_root, name);

                for (var attempt = 2; Directory.Exists(path); attempt++)
                {
                    path = Path.Combine(_root, $"{name}-{attempt}");
                }

                Directory.CreateDirectory(path);

                return path;
            }
            catch (Exception exception)
            {
                throw new PortalException(
                    PortalErrorCode.ExportFailed,
                    $"No backup directory could be created under '{_root}': {exception.Message}",
                    null,
                    exception);
            }
        }

        private static void WriteManifest(string path, string tool, string target, DateTimeOffset takenAt)
        {
            var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tool"] = tool,
                ["target"] = target,
                ["takenAt"] = takenAt.ToString("o", CultureInfo.InvariantCulture)
            };

            try
            {
                File.WriteAllText(
                    Path.Combine(path, ManifestName),
                    JsonSerializer.Serialize(manifest),
                    new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                // Not swallowed, and not merely cosmetic: without the manifest the directory cannot
                // be attributed to a tool or a target, so it would be a pile of exported blocks
                // nobody can place. Refusing here means the write has not happened yet.
                throw new PortalException(
                    PortalErrorCode.ExportFailed,
                    $"The backup manifest in '{path}' could not be written: {exception.Message}",
                    null,
                    exception);
            }
        }

        private static BackupRecord? ReadRecord(string directory)
        {
            var manifestPath = Path.Combine(directory, ManifestName);

            if (!File.Exists(manifestPath))
            {
                // Not ours, or not finished being created. Either way it is not a backup this
                // registry can describe, and inventing a tool and target for it would be worse
                // than leaving it out.
                return null;
            }

            var manifest = ReadManifest(manifestPath);

            if (manifest == null)
            {
                return null;
            }

            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length - 1;

            return new BackupRecord(
                directory,
                Field(manifest, "tool"),
                Field(manifest, "target"),
                ParseTakenAt(Field(manifest, "takenAt")),
                Math.Max(0, files));
        }

        private static Dictionary<string, string>? ReadManifest(string manifestPath)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath));
            }
            catch (Exception)
            {
                // A manifest half-written by a process that died is a directory we cannot describe,
                // not a reason to fail the whole listing. Every other backup still lists.
                return null;
            }
        }

        private static DateTimeOffset ParseTakenAt(string value)
        {
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var takenAt)
                ? takenAt
                : DateTimeOffset.MinValue;
        }

        private static string Field(Dictionary<string, string> manifest, string name)
        {
            return manifest.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;
        }

        private static string Sanitise(string value)
        {
            var sanitised = new StringBuilder(value.Length);

            foreach (var character in value)
            {
                sanitised.Append(char.IsLetterOrDigit(character) || character == '.' || character == '-'
                    ? character
                    : '_');
            }

            // Truncated because a target is a full project path and Windows still has a path limit
            // the audit trail does not. The manifest keeps the untruncated target, so nothing is
            // lost — only the directory name is short.
            return sanitised.Length > MaxTargetLength
                ? sanitised.ToString(0, MaxTargetLength)
                : sanitised.ToString();
        }
    }
}
