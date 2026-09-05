using System;
using System.Collections.Generic;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Collects the outcome of a snapshot export while it runs, so the exporter itself keeps no
    /// mutable state between calls and stays reentrant.
    /// </summary>
    internal sealed class SnapshotReportBuilder
    {
        private readonly string _rootDirectory;
        private readonly List<string> _exported = new List<string>();
        private readonly List<string> _inconsistent = new List<string>();
        private readonly List<string> _unsupported = new List<string>();
        private readonly List<string> _failed = new List<string>();

        // Every path this run has already written to, so a second item cannot land on the first
        // one's file. Ordinal-ignore-case because that is how the file system compares them.
        private readonly HashSet<string> _claimed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal SnapshotReportBuilder(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
        }

        /// <remarks>
        /// Two TIA names can map to one file name -- see <see cref="SnapshotFileName"/> -- and the
        /// exporter deletes whatever is already at the path before writing. Without this, the second
        /// block replaced the first and the report claimed both, which makes a snapshot that is
        /// missing a block and does not say so.
        ///
        /// Refused rather than renamed: a snapshot exists to be diffed against the previous one, and
        /// silently renaming files would produce a diff nobody can read. This way the collision is a
        /// line in the failure list, naming both items.
        /// </remarks>
        internal bool TryClaim(FileInfo file, string itemName)
        {
            if (_claimed.Add(file.FullName))
            {
                return true;
            }

            AddFailure(
                itemName,
                $"another item in this snapshot already wrote '{ToRelativePath(file.FullName)}'. " +
                "Their names differ only in characters a file name cannot carry.");

            return false;
        }

        internal void AddExported(FileInfo file)
        {
            _exported.Add(ToRelativePath(file.FullName));
        }

        internal void AddInconsistent(string itemName)
        {
            _inconsistent.Add(itemName);
        }

        internal void AddUnsupported(string blockName, string programmingLanguage)
        {
            _unsupported.Add($"{blockName} ({programmingLanguage})");
        }

        internal void AddFailure(string itemName, string reason)
        {
            _failed.Add($"{itemName}: {reason}");
        }

        internal SnapshotResult Build()
        {
            return new SnapshotResult(_exported, _inconsistent, _unsupported, _failed);
        }

        private string ToRelativePath(string fullPath)
        {
            var root = _rootDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            var relative = fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(root.Length)
                : fullPath;

            // Forward slashes so a snapshot report reads identically wherever it was produced.
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
