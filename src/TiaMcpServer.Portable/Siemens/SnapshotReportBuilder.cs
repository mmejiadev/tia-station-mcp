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

        internal SnapshotReportBuilder(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
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
