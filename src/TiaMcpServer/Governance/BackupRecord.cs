using System;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// One backup the registry holds.
    /// </summary>
    /// <remarks>
    /// A fact that already happened, so nothing here has a setter. <see cref="FileCount"/> is read
    /// from disk rather than recorded at allocation time, which is what makes an allocation whose
    /// export never ran visible as what it is: a backup directory holding nothing.
    /// </remarks>
    public sealed class BackupRecord
    {
        /// <summary>Describes one backup.</summary>
        /// <param name="path">The directory holding it.</param>
        /// <param name="tool">The tool the backup was taken for.</param>
        /// <param name="target">What that tool was about to write to.</param>
        /// <param name="takenAt">When the directory was reserved, in UTC.</param>
        /// <param name="fileCount">How many files it holds, counted now.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
        public BackupRecord(string path, string tool, string target, DateTimeOffset takenAt, int fileCount)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A backup record needs a path", nameof(path));
            }

            Path = path;
            Tool = tool ?? string.Empty;
            Target = target ?? string.Empty;
            TakenAt = takenAt;
            FileCount = fileCount;
        }

        /// <summary>The directory holding the backup.</summary>
        public string Path { get; }

        /// <summary>The tool it was taken for.</summary>
        public string Tool { get; }

        /// <summary>What that tool was about to write to.</summary>
        public string Target { get; }

        /// <summary>When the directory was reserved, in UTC.</summary>
        public DateTimeOffset TakenAt { get; }

        /// <summary>How many files it holds. Zero means the write never got as far as exporting.</summary>
        public int FileCount { get; }

        /// <summary>Whether anything was actually saved here.</summary>
        public bool IsEmpty => FileCount == 0;
    }
}
