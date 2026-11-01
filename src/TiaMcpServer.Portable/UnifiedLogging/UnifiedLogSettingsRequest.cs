using System;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>The settings asked for one log: only the ones given change, the rest stay as they are.</summary>
    public sealed class UnifiedLogSettingsRequest
    {
        /// <summary>Creates a request.</summary>
        /// <param name="name">The data log or alarm log; the two share one set of names.</param>
        /// <param name="log">The log's maximum size and period, each null to keep it.</param>
        /// <param name="segment">A segment's maximum size, period and start, each null to keep it.</param>
        /// <param name="backupPath">Where to back the log up, empty for no backup, null to keep it.</param>
        public UnifiedLogSettingsRequest(string name, (uint? MaxSize, UnifiedLogDuration? Duration) log, (uint? MaxSize, UnifiedLogDuration? Duration, DateTime? Start) segment, string? backupPath)
        {
            Name = name;
            MaxSize = log.MaxSize;
            Duration = log.Duration;
            SegmentMaxSize = segment.MaxSize;
            SegmentDuration = segment.Duration;
            SegmentStart = segment.Start;
            BackupPath = backupPath;
        }

        /// <summary>The log.</summary>
        public string Name { get; }

        /// <summary>The log's maximum size, or null to keep it.</summary>
        public uint? MaxSize { get; }

        /// <summary>The log's period, or null to keep it.</summary>
        public UnifiedLogDuration? Duration { get; }

        /// <summary>A segment's maximum size, or null to keep it.</summary>
        public uint? SegmentMaxSize { get; }

        /// <summary>A segment's period, or null to keep it.</summary>
        public UnifiedLogDuration? SegmentDuration { get; }

        /// <summary>When the first segment starts, or null to keep it.</summary>
        public DateTime? SegmentStart { get; }

        /// <summary>Where to back the log up, empty for no backup, or null to keep it.</summary>
        public string? BackupPath { get; }

        /// <summary>The settings this request leaves a log with.</summary>
        /// <param name="current">The log's settings now.</param>
        /// <returns>Each setting given, and the current one for each not given.</returns>
        /// <remarks>Turning the backup off keeps the path stored, as TIA Portal does; a path turns it on.</remarks>
        public UnifiedLogSettings Over(UnifiedLogSettings current)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            var backup = BackupPath == null
                ? (current.IsBackedUp, current.BackupPath)
                : BackupPath.Length == 0 ? (false, current.BackupPath) : (true, BackupPath);

            return new UnifiedLogSettings(
                (MaxSize ?? current.MaxSize, Duration ?? current.Duration),
                (SegmentMaxSize ?? current.SegmentMaxSize, SegmentDuration ?? current.SegmentDuration, SegmentStart ?? current.SegmentStart),
                backup);
        }

        /// <summary>Whether this request changes anything at all.</summary>
        public bool IsEmpty => MaxSize == null && Duration == null && SegmentMaxSize == null && SegmentDuration == null && SegmentStart == null && BackupPath == null;
    }
}
