namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>One setting of a log that Openness writes on its own.</summary>
    public enum UnifiedLogField
    {
        /// <summary>The log's maximum size.</summary>
        MaxSize,

        /// <summary>The days of the log's period.</summary>
        Days,

        /// <summary>The hours of the log's period.</summary>
        Hours,

        /// <summary>The minutes of the log's period.</summary>
        Minutes,

        /// <summary>The seconds of the log's period.</summary>
        Seconds,

        /// <summary>A segment's maximum size.</summary>
        SegmentMaxSize,

        /// <summary>The days of a segment's period.</summary>
        SegmentDays,

        /// <summary>The hours of a segment's period.</summary>
        SegmentHours,

        /// <summary>The minutes of a segment's period.</summary>
        SegmentMinutes,

        /// <summary>The seconds of a segment's period.</summary>
        SegmentSeconds,

        /// <summary>When the first segment starts.</summary>
        SegmentStart,

        /// <summary>The backup's path.</summary>
        BackupPath,

        /// <summary>Whether the log is backed up to that path.</summary>
        IsBackedUp
    }
}
