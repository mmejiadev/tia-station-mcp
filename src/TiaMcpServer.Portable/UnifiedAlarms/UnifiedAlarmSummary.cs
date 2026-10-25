using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>The change plan's line for an alarm list: every class and alarm, with what raises it.</summary>
    /// <remarks>Creating alarms takes no backup, so the plan is the only record of what was asked for.</remarks>
    public static class UnifiedAlarmSummary
    {
        /// <summary>Describes a list.</summary>
        /// <param name="list">The list.</param>
        /// <returns>E.g. <c>class Tolva; discrete Tolva_Plena (Alarms bit 3, Tolva, 'Tolva plena')</c>.</returns>
        public static string Of(UnifiedAlarmList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var parts = new List<string>();
            parts.AddRange(list.Classes.Select(Class));
            parts.AddRange(list.Discrete.Select(alarm => $"discrete {alarm.Common.Name} ({alarm.Tag} bit {alarm.Bit}{(alarm.Trigger == UnifiedAlarmTrigger.FallingEdge ? " falling" : string.Empty)}, {Common(alarm.Common)})"));
            parts.AddRange(list.Analog.Select(alarm => $"analog {alarm.Common.Name} ({alarm.Tag} {alarm.Condition} {alarm.Value}, {Common(alarm.Common)})"));

            return string.Join("; ", parts);
        }

        private static string Class(UnifiedAlarmClassDescription found)
        {
            var parts = new List<string> { $"class {found.Name}" };
            if (found.Priority != null)
            {
                parts.Add($"priority {found.Priority}");
            }

            if (found.StateMachine != null)
            {
                parts.Add(found.StateMachine.Value.ToString());
            }

            parts.AddRange(found.States.Select(state => $"{state.Key} {Visuals(state.Value)}"));

            return string.Join(" ", parts);
        }

        private static string Visuals(UnifiedAlarmStateVisuals visuals)
        {
            var parts = new List<string>();
            if (visuals.BackColor != null)
            {
                parts.Add($"back {visuals.BackColor}");
            }

            if (visuals.TextColor != null)
            {
                parts.Add($"text {visuals.TextColor}");
            }

            if (visuals.Flashing != null)
            {
                parts.Add(visuals.Flashing.Value ? "flashing" : "steady");
            }

            return $"[{string.Join(", ", parts)}]";
        }

        private static string Common(UnifiedAlarmCommon common)
        {
            var parts = new List<string> { common.AlarmClass };
            if (common.Priority != null)
            {
                parts.Add($"priority {common.Priority}");
            }

            parts.Add($"'{common.Texts.Text}'");
            parts.AddRange(new[] { ("info", common.Texts.Info), ("origin", common.Texts.Origin), ("area", common.Texts.Area) }
                .Where(entry => entry.Item2 != null)
                .Select(entry => $"{entry.Item1} '{entry.Item2}'"));

            return string.Join(", ", parts);
        }
    }
}
