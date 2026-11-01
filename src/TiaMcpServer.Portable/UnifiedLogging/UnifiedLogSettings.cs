using System;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>Every setting of one data log or alarm log this server reads and writes.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20, on a new log: maximum size 1000 and period 7 days, segments of
    /// 100 and 1 day, no backup. The storage medium is left alone: on a PC runtime only <c>Default</c> and
    /// <c>Local</c> are taken, and <c>Local</c> needs a storage folder Openness V20 holds read-only, which the
    /// compile then refuses as empty. The backup is kept as TIA Portal holds it, a switch and a path apart:
    /// a path stays stored while the backup is off, and a backup can be on with no path.
    /// </remarks>
    public sealed class UnifiedLogSettings
    {
        /// <summary>Creates a full set of settings.</summary>
        /// <param name="log">The log's maximum size, as TIA Portal counts it, and its period.</param>
        /// <param name="segment">A segment's maximum size and period, and when the first starts.</param>
        /// <param name="backup">Whether the log is backed up, and the path stored for it, kept even while off.</param>
        public UnifiedLogSettings((uint MaxSize, UnifiedLogDuration Duration) log, (uint MaxSize, UnifiedLogDuration Duration, DateTime Start) segment, (bool IsOn, string Path) backup)
        {
            MaxSize = log.MaxSize;
            Duration = log.Duration ?? throw new ArgumentNullException(nameof(log));
            SegmentMaxSize = segment.MaxSize;
            SegmentDuration = segment.Duration ?? throw new ArgumentNullException(nameof(segment));
            SegmentStart = segment.Start;
            IsBackedUp = backup.IsOn;
            BackupPath = backup.Path ?? string.Empty;
        }

        /// <summary>The log's maximum size, as TIA Portal counts it (1000 on a new log).</summary>
        public uint MaxSize { get; }

        /// <summary>How long the log keeps its data.</summary>
        public UnifiedLogDuration Duration { get; }

        /// <summary>A segment's maximum size, never above <see cref="MaxSize"/>.</summary>
        public uint SegmentMaxSize { get; }

        /// <summary>How long a segment spans, never above <see cref="Duration"/>.</summary>
        public UnifiedLogDuration SegmentDuration { get; }

        /// <summary>When the first segment starts.</summary>
        public DateTime SegmentStart { get; }

        /// <summary>Whether the log is backed up.</summary>
        public bool IsBackedUp { get; }

        /// <summary>The path stored for the backup, kept even while <see cref="IsBackedUp"/> is false.</summary>
        public string BackupPath { get; }
    }
}
