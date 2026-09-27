namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One backup the registry holds.</summary>
    public sealed class ResponseBackup
    {
        /// <summary>Describes one backup.</summary>
        /// <param name="path">The directory holding it.</param>
        /// <param name="tool">The tool it was taken for.</param>
        /// <param name="target">What that tool was about to write to.</param>
        /// <param name="takenAt">When it was taken, in round-trip UTC.</param>
        /// <param name="fileCount">How many files it holds.</param>
        public ResponseBackup(string path, string tool, string target, string takenAt, int fileCount)
        {
            Path = path;
            Tool = tool;
            Target = target;
            TakenAt = takenAt;
            FileCount = fileCount;
        }

        /// <summary>The directory holding the backup.</summary>
        public string Path { get; }

        /// <summary>The tool it was taken for.</summary>
        public string Tool { get; }

        /// <summary>What that tool was about to write to.</summary>
        public string Target { get; }

        /// <summary>When it was taken, in round-trip UTC.</summary>
        public string TakenAt { get; }

        /// <summary>
        /// How many files it holds. Zero means the change was refused or failed before exporting,
        /// so there is nothing here to restore from.
        /// </summary>
        public int FileCount { get; }
    }
}
