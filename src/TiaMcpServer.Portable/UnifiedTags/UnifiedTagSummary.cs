using System;
using System.Collections.Generic;
using System.Globalization;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>One line per tag for the change plan, settings included.</summary>
    /// <remarks>
    /// Creating tags takes no backup, so the plan is the only record of what was asked for: it names each
    /// tag, its type or link, and every setting with its value.
    /// </remarks>
    public static class UnifiedTagSummary
    {
        /// <summary>Describes one tag.</summary>
        /// <param name="tag">The tag.</param>
        /// <returns>E.g. <c>Level (Int; limits 0..100; comment 'Tank')</c>.</returns>
        public static string Of(UnifiedTagDescription tag)
        {
            if (tag == null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            var parts = new List<string> { tag.Link == null ? tag.DataType! : $"-> {tag.Link.Connection}/{tag.Link.PlcTag}" };
            parts.AddRange(Options(tag.Settings.Options));
            parts.AddRange(Limits(tag.Settings.Limits));
            parts.AddRange(PlcReading(tag.Settings.PlcReading));

            return $"{tag.Name} ({string.Join("; ", parts)})";
        }

        private static IEnumerable<string> Options(UnifiedTagOptions options)
        {
            if (options.Comment != null)
            {
                yield return $"comment '{options.Comment}'";
            }

            if (options.InitialValue != null)
            {
                yield return $"start {options.InitialValue}";
            }

            if (options.Persistent != null)
            {
                yield return options.Persistent.Value ? "retained" : "not retained";
            }

            if (options.MaxLength != null)
            {
                yield return $"length {options.MaxLength}";
            }
        }

        private static IEnumerable<string> Limits(UnifiedTagLimits limits)
        {
            if (limits.IsSet)
            {
                yield return $"limits {Limit(limits.Lower)}..{Limit(limits.Upper)}";
            }
        }

        private static string Limit(UnifiedTagLimit? limit)
        {
            if (limit == null)
            {
                return string.Empty;
            }

            return limit.IsTag ? $"tag {limit.Value}" : limit.Value;
        }

        private static IEnumerable<string> PlcReading(UnifiedTagPlcReading reading)
        {
            if (reading.Acquisition != null)
            {
                yield return $"acquisition {reading.Acquisition.Mode}{(reading.Acquisition.Cycle == null ? string.Empty : " " + reading.Acquisition.Cycle)}";
            }

            if (reading.Scaling != null)
            {
                yield return $"scaling PLC {Number(reading.Scaling.Plc.Start)}..{Number(reading.Scaling.Plc.End)} to HMI {Number(reading.Scaling.Hmi.Start)}..{Number(reading.Scaling.Hmi.End)}";
            }

            if (reading.Substitute != null)
            {
                yield return $"substitute {reading.Substitute.Value} on {reading.Substitute.Usage}";
            }
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
