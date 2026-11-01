using Siemens.Engineering.HmiUnified.HmiLogging.HmiLoggingCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a WinCC Unified log's settings, and writes them one field at a time.</summary>
    /// <remarks>
    /// Data logs and alarm logs carry the same three parts (measured 2026-10-30): <c>Settings</c> with the
    /// maximum size and period, <c>Segment</c> with a segment's size, period and start, and <c>Backup</c> with
    /// a mode and a path. Both are read as they are: a path stays stored under <c>NoBackup</c>, and a backup
    /// can be on with no path, so neither is folded into the other.
    /// </remarks>
    internal static class UnifiedLogSettingsAccess
    {
        /// <summary>Reads a log's settings.</summary>
        /// <param name="log">A data log or an alarm log.</param>
        /// <returns>The settings.</returns>
        public static UnifiedLogSettings Read(LoggingBase log)
        {
            var period = log.Settings.LogTimePeriod;
            var segmentPeriod = log.Segment.SegmentTimePeriod;

            return new UnifiedLogSettings(
                (log.Settings.LogMaxSize, new UnifiedLogDuration(period.Days, period.Hours, period.Minutes, period.Seconds)),
                (log.Segment.SegmentMaxSize, new UnifiedLogDuration(segmentPeriod.Days, segmentPeriod.Hours, segmentPeriod.Minutes, segmentPeriod.Seconds), log.Segment.SegmentStartTime),
                (log.Backup.BackupMode == HmiBackupMode.PrimaryPath, log.Backup.PrimaryPath ?? string.Empty));
        }

        /// <summary>Describes a log's settings exactly, for the backup registry: the backup switch and the stored path apart.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns><see cref="Describe"/>'s shape, with <c>backupOn</c> and <c>storedBackupPath</c>.</returns>
        public static JsonObject DescribeExactly(UnifiedLogSettings settings)
        {
            var described = Describe(settings);
            described["backupOn"] = settings.IsBackedUp;
            described["storedBackupPath"] = settings.BackupPath;

            return described;
        }

        /// <summary>Describes a log's settings as the tools read and take them.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns><c>{ maxSize, duration, segment: { maxSize, duration, start }, backupPath }</c>; <c>backupPath</c> is empty while the backup is off.</returns>
        public static JsonObject Describe(UnifiedLogSettings settings)
        {
            return new JsonObject
            {
                ["maxSize"] = settings.MaxSize,
                ["duration"] = settings.Duration.ToJson(),
                ["segment"] = new JsonObject
                {
                    ["maxSize"] = settings.SegmentMaxSize,
                    ["duration"] = settings.SegmentDuration.ToJson(),
                    ["start"] = settings.SegmentStart.ToString("s", CultureInfo.InvariantCulture)
                },
                ["backupPath"] = settings.IsBackedUp ? settings.BackupPath : string.Empty
            };
        }

        /// <summary>Writes one field.</summary>
        /// <param name="log">The log.</param>
        /// <param name="step">The field and its value, as <see cref="UnifiedLogSettingsPlan.Steps"/> gives it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for a field this server does not write: never guessed at.</exception>
        public static void Write(LoggingBase log, (UnifiedLogField Field, object Value) step)
        {
            if (!Setters.TryGetValue(step.Field, out var set))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised log setting: {step.Field}");
            }

            set(log, step.Value);
        }

        /// <remarks>A field added to <see cref="UnifiedLogField"/> without a setter here is refused by <see cref="Write"/>, never skipped.</remarks>
        private static readonly Dictionary<UnifiedLogField, Action<LoggingBase, object>> Setters = new Dictionary<UnifiedLogField, Action<LoggingBase, object>>
        {
            [UnifiedLogField.MaxSize] = (log, value) => log.Settings.LogMaxSize = (uint)value,
            [UnifiedLogField.Days] = (log, value) => log.Settings.LogTimePeriod.Days = (uint)value,
            [UnifiedLogField.Hours] = (log, value) => log.Settings.LogTimePeriod.Hours = (uint)value,
            [UnifiedLogField.Minutes] = (log, value) => log.Settings.LogTimePeriod.Minutes = (uint)value,
            [UnifiedLogField.Seconds] = (log, value) => log.Settings.LogTimePeriod.Seconds = (uint)value,
            [UnifiedLogField.SegmentMaxSize] = (log, value) => log.Segment.SegmentMaxSize = (uint)value,
            [UnifiedLogField.SegmentDays] = (log, value) => log.Segment.SegmentTimePeriod.Days = (uint)value,
            [UnifiedLogField.SegmentHours] = (log, value) => log.Segment.SegmentTimePeriod.Hours = (uint)value,
            [UnifiedLogField.SegmentMinutes] = (log, value) => log.Segment.SegmentTimePeriod.Minutes = (uint)value,
            [UnifiedLogField.SegmentSeconds] = (log, value) => log.Segment.SegmentTimePeriod.Seconds = (uint)value,
            [UnifiedLogField.SegmentStart] = (log, value) => log.Segment.SegmentStartTime = (DateTime)value,
            [UnifiedLogField.BackupPath] = (log, value) => log.Backup.PrimaryPath = (string)value,
            [UnifiedLogField.IsBackedUp] = (log, value) => log.Backup.BackupMode = (bool)value ? HmiBackupMode.PrimaryPath : HmiBackupMode.NoBackup
        };
    }
}
