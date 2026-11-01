using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>Checks a log's settings and orders the writes that take a log from one set to another.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: every single write is refused that would leave a segment larger
    /// than its log, in size ("The maximum size of the segment must not exceed the maximum size of the log")
    /// or in time ("The time period of a segment must not exceed the time period of the log"), even when the
    /// writes after it would end valid. So the log is first widened past both the old and the new segment,
    /// the segment is written, and the log is narrowed to its own value last, smaller parts of its period
    /// first. A backup to an empty path is taken on write and refused only by the compile ("The primary
    /// backup path of the log is empty"), so it is refused here.
    /// </remarks>
    public static class UnifiedLogSettingsPlan
    {
        /// <summary>Throws unless the settings are ones TIA Portal keeps and compiles.</summary>
        /// <param name="name">The log, named in a refusal.</param>
        /// <param name="settings">The settings it would have.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming every problem.</exception>
        public static void Check(string name, UnifiedLogSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var problems = new List<string>();

            if (settings.MaxSize == 0 || settings.Duration.TotalSeconds == 0)
            {
                problems.Add("the log needs a maximum size and a period above 0");
            }

            if (settings.SegmentMaxSize == 0 || settings.SegmentDuration.TotalSeconds == 0)
            {
                problems.Add("a segment needs a maximum size and a period above 0");
            }

            if (settings.IsBackedUp && settings.BackupPath.Length == 0)
            {
                problems.Add("the backup is on with no path, which the compile refuses (\"The primary backup path of the log is empty\"); give a backupPath, or \"\" to turn it off");
            }

            if (settings.SegmentMaxSize > settings.MaxSize)
            {
                problems.Add($"a segment's maximum size ({settings.SegmentMaxSize}) is above the log's ({settings.MaxSize})");
            }

            if (settings.SegmentDuration.TotalSeconds > settings.Duration.TotalSeconds)
            {
                problems.Add($"a segment's period ({settings.SegmentDuration}) is longer than the log's ({settings.Duration})");
            }

            if (problems.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Log '{name}': {string.Join("; ", problems)}");
            }
        }

        /// <summary>The writes that take a log from its settings now to the ones asked, in an order TIA Portal takes.</summary>
        /// <param name="current">The settings now.</param>
        /// <param name="target">The settings asked, checked by <see cref="Check"/>.</param>
        /// <returns>Each write, with the value it writes; none for a setting already right.</returns>
        public static IReadOnlyList<(UnifiedLogField Field, object Value)> Steps(UnifiedLogSettings current, UnifiedLogSettings target)
        {
            if (current == null || target == null)
            {
                throw new ArgumentNullException(current == null ? nameof(current) : nameof(target));
            }

            var steps = new List<(UnifiedLogField Field, object Value)>();

            // Wider than both segments, old and new, so no segment write is ever above the log. The days are
            // widened for any change of a period, the log's included: a log growing from 1d 3h 59m to 1d 4h
            // would pass 1d 3h 0m on the way, below a segment of 1d 3h 59m.
            if (current.MaxSize != target.MaxSize || current.SegmentMaxSize != target.SegmentMaxSize)
            {
                steps.Add((UnifiedLogField.MaxSize, Math.Max(current.MaxSize, target.MaxSize)));
            }

            if (current.Duration.TotalSeconds != target.Duration.TotalSeconds || current.SegmentDuration.TotalSeconds != target.SegmentDuration.TotalSeconds)
            {
                steps.Add((UnifiedLogField.Days, Math.Max(current.Duration.Days, target.Duration.Days) + 1));
            }

            steps.AddRange(Segment(target));
            steps.AddRange(Log(target));
            steps.AddRange(Backup(current, target));

            return WithoutWritesAlreadyRight(current, steps);
        }

        private static IEnumerable<(UnifiedLogField, object)> Segment(UnifiedLogSettings target)
        {
            yield return (UnifiedLogField.SegmentMaxSize, target.SegmentMaxSize);
            yield return (UnifiedLogField.SegmentDays, target.SegmentDuration.Days);
            yield return (UnifiedLogField.SegmentHours, target.SegmentDuration.Hours);
            yield return (UnifiedLogField.SegmentMinutes, target.SegmentDuration.Minutes);
            yield return (UnifiedLogField.SegmentSeconds, target.SegmentDuration.Seconds);
            yield return (UnifiedLogField.SegmentStart, target.SegmentStart);
        }

        /// <remarks>Seconds first and days last: while the days are still wide, narrowing a smaller part never drops the log below the segment.</remarks>
        private static IEnumerable<(UnifiedLogField, object)> Log(UnifiedLogSettings target)
        {
            yield return (UnifiedLogField.Seconds, target.Duration.Seconds);
            yield return (UnifiedLogField.Minutes, target.Duration.Minutes);
            yield return (UnifiedLogField.Hours, target.Duration.Hours);
            yield return (UnifiedLogField.Days, target.Duration.Days);
            yield return (UnifiedLogField.MaxSize, target.MaxSize);
        }

        /// <remarks>
        /// Measured 2026-10-30 against TIA Portal V20: the path is read-only while the backup is off ("The
        /// property 'PrimaryPath' is read-only"), and a backup on with an empty path is taken on write. So the
        /// backup is switched on before its path is written, and a path to keep while off is written before it is
        /// switched off. A path cannot change between two settings that are both off; none asked here does.
        /// </remarks>
        private static IEnumerable<(UnifiedLogField, object)> Backup(UnifiedLogSettings current, UnifiedLogSettings target)
        {
            if (target.IsBackedUp)
            {
                yield return (UnifiedLogField.IsBackedUp, true);
                yield return (UnifiedLogField.BackupPath, target.BackupPath);
                yield break;
            }

            if (current.IsBackedUp)
            {
                yield return (UnifiedLogField.BackupPath, target.BackupPath);
            }

            yield return (UnifiedLogField.IsBackedUp, false);
        }

        private static List<(UnifiedLogField Field, object Value)> WithoutWritesAlreadyRight(UnifiedLogSettings current, List<(UnifiedLogField Field, object Value)> steps)
        {
            var values = UnifiedLogFieldValues.Of(current);
            var kept = new List<(UnifiedLogField Field, object Value)>();

            foreach (var step in steps.Where(step => !Equals(values[step.Field], step.Value)))
            {
                kept.Add(step);
                values[step.Field] = step.Value;
            }

            return kept;
        }
    }
}
