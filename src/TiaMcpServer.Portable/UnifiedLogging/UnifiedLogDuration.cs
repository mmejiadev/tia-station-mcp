using System;
using System.Text.Json.Nodes;

namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>A log's or a segment's time period, as TIA Portal holds it: days, hours, minutes and seconds.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: each part is set on its own, and hours above 23, minutes above 59
    /// and seconds above 59 are refused ("Unable to set PropertyValue"). The <c>Ticks</c> part reads back a
    /// million times what was written, so it is neither written nor read here.
    /// </remarks>
    public sealed class UnifiedLogDuration
    {
        /// <summary>The most days asked for: one short of 100000, the most measured to be taken.</summary>
        /// <remarks>
        /// Measured 2026-10-30 on TIA Portal V20: 100000 days are taken, 2147483647 refused ("Unable to set
        /// PropertyValue"); the limit between was not looked for. One short, because a write plan widens the log
        /// by one day while it writes the segment (see <see cref="UnifiedLogSettingsPlan"/>).
        /// </remarks>
        public const uint MaxDays = 99999;

        /// <summary>The highest hours TIA Portal takes.</summary>
        public const uint MaxHours = 23;

        /// <summary>The highest minutes or seconds TIA Portal takes.</summary>
        public const uint MaxMinutesOrSeconds = 59;

        /// <summary>Creates a period.</summary>
        /// <param name="days">Days.</param>
        /// <param name="hours">Hours, 0 to 23.</param>
        /// <param name="minutes">Minutes, 0 to 59.</param>
        /// <param name="seconds">Seconds, 0 to 59.</param>
        public UnifiedLogDuration(uint days, uint hours, uint minutes, uint seconds)
        {
            Days = days;
            Hours = hours;
            Minutes = minutes;
            Seconds = seconds;
        }

        /// <summary>Days.</summary>
        public uint Days { get; }

        /// <summary>Hours, 0 to 23.</summary>
        public uint Hours { get; }

        /// <summary>Minutes, 0 to 59.</summary>
        public uint Minutes { get; }

        /// <summary>Seconds, 0 to 59.</summary>
        public uint Seconds { get; }

        /// <summary>The whole period in seconds, to compare two.</summary>
        public ulong TotalSeconds => (((ulong)Days * 24 + Hours) * 60 + Minutes) * 60 + Seconds;

        /// <summary>Writes the period as the tools read and take it.</summary>
        /// <returns><c>{ days, hours, minutes, seconds }</c>.</returns>
        public JsonObject ToJson()
        {
            return new JsonObject { ["days"] = Days, ["hours"] = Hours, ["minutes"] = Minutes, ["seconds"] = Seconds };
        }

        /// <summary>A short text for a plan or a refusal.</summary>
        /// <returns>E.g. <c>30d 0h 0m 0s</c>.</returns>
        public override string ToString()
        {
            return FormattableString.Invariant($"{Days}d {Hours}h {Minutes}m {Seconds}s");
        }
    }
}
