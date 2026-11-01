using System;
using System.Collections.Generic;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>A log's settings as one value per field, the shape the write plan compares and a writer sets.</summary>
    public static class UnifiedLogFieldValues
    {
        /// <summary>Splits a set of settings into its fields.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns>Every <see cref="UnifiedLogField"/> with its value: a <c>uint</c>, a <c>DateTime</c>, a string or a bool.</returns>
        public static Dictionary<UnifiedLogField, object> Of(UnifiedLogSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return new Dictionary<UnifiedLogField, object>
            {
                [UnifiedLogField.MaxSize] = settings.MaxSize,
                [UnifiedLogField.Days] = settings.Duration.Days,
                [UnifiedLogField.Hours] = settings.Duration.Hours,
                [UnifiedLogField.Minutes] = settings.Duration.Minutes,
                [UnifiedLogField.Seconds] = settings.Duration.Seconds,
                [UnifiedLogField.SegmentMaxSize] = settings.SegmentMaxSize,
                [UnifiedLogField.SegmentDays] = settings.SegmentDuration.Days,
                [UnifiedLogField.SegmentHours] = settings.SegmentDuration.Hours,
                [UnifiedLogField.SegmentMinutes] = settings.SegmentDuration.Minutes,
                [UnifiedLogField.SegmentSeconds] = settings.SegmentDuration.Seconds,
                [UnifiedLogField.SegmentStart] = settings.SegmentStart,
                [UnifiedLogField.BackupPath] = settings.BackupPath,
                [UnifiedLogField.IsBackedUp] = settings.IsBackedUp
            };
        }
    }
}
