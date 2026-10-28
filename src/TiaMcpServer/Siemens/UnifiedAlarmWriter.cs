using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiAlarm.HmiAlarmCommon;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using TiaMcpServer.UnifiedAlarms;
using TiaMcpServer.UnifiedScreens;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Creates WinCC Unified alarm classes and alarms, all of a list or none of it.</summary>
    /// <remarks>
    /// Measured 2026-10-25 on TIA Portal V20. An alarm's texts are formatted text, a plain one being refused
    /// as "an invalid format"; an analog value is a number, Openness refusing it as text; an alarm's id is
    /// TIA Portal's to give, and a repeated one is refused. Deleting a class that alarms use succeeds and
    /// leaves them naming a class that is gone, so this tool never deletes a class once alarms exist; an
    /// undo deletes the alarms first.
    /// </remarks>
    internal sealed class UnifiedAlarmWriter
    {
        private readonly UnifiedHmiSoftware _hmi;
        private readonly Language _language;

        /// <summary>Every class an alarm may use, by name in any case, filled by <see cref="Check"/> and by the classes made.</summary>
        private readonly Dictionary<string, string> _classNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a writer for one HMI.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="language">The project's editing language, the one texts are written in.</param>
        public UnifiedAlarmWriter(UnifiedHmiSoftware hmi, Language language)
        {
            _hmi = hmi;
            _language = language;
        }

        /// <summary>Refuses a list that cannot be created as it stands, before anything is changed.</summary>
        /// <param name="list">The classes and alarms.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> naming every problem found.</exception>
        public void Check(UnifiedAlarmList list)
        {
            var checker = new UnifiedAlarmChecker(_hmi);
            var problems = checker.Problems(list);
            foreach (var name in checker.ClassNames)
            {
                _classNames[name] = name;
            }

            if (problems.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was created: {string.Join("; ", problems)}");
            }
        }

        /// <summary>Creates the classes, then the alarms; undoes all of it when one fails.</summary>
        /// <param name="list">The classes and alarms, already passed through <see cref="Check"/>.</param>
        /// <returns>The names of what was created, by kind.</returns>
        public (IReadOnlyList<string> Classes, IReadOnlyList<string> Discrete, IReadOnlyList<string> Analog) Create(UnifiedAlarmList list)
        {
            var made = new List<(string Name, Action Delete)>();

            try
            {
                foreach (var found in list.Classes)
                {
                    Refusing($"alarm class '{found.Name}'", made, () => _hmi.AlarmClasses.Create(found.Name), created => created.Delete, created => WriteClass(created, found));
                    _classNames[found.Name] = found.Name;
                }

                foreach (var alarm in list.Discrete)
                {
                    Refusing($"discrete alarm '{alarm.Common.Name}'", made, () => _hmi.DiscreteAlarms.Create(alarm.Common.Name), created => created.Delete, created => WriteDiscrete(created, alarm));
                }

                foreach (var alarm in list.Analog)
                {
                    Refusing($"analog alarm '{alarm.Common.Name}'", made, () => _hmi.AnalogAlarms.Create(alarm.Common.Name), created => created.Delete, created => WriteAnalog(created, alarm));
                }

                return (list.Classes.Select(found => found.Name).ToList(), list.Discrete.Select(alarm => alarm.Common.Name).ToList(), list.Analog.Select(alarm => alarm.Common.Name).ToList());
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw UnifiedRollback.Undo(made, failure, "classes and alarms");
            }
        }

        /// <remarks>
        /// Creates one object and writes it; what TIA Portal refuses is a bad request, reported with the
        /// object it was about, so a long list says which entry to fix. The object goes in the undo list as
        /// soon as it exists, so a refused write removes it too.
        /// </remarks>
        private static void Refusing<T>(string what, List<(string Name, Action Delete)> made, Func<T> create, Func<T, Action> delete, Action<T> write)
        {
            try
            {
                var created = create();
                made.Add((what, delete(created)));
                write(created);
            }
            catch (Exception failure) when (!(failure is PortalException) && !UnifiedRollback.IsSessionEnded(failure))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused {what}: {failure.Message.Trim()}", null, failure);
            }
        }

        private static void WriteClass(HmiAlarmClass target, UnifiedAlarmClassDescription found)
        {
            if (found.Priority != null)
            {
                target.Priority = found.Priority.Value;
            }

            if (found.StateMachine != null)
            {
                target.StateMachine = ToOpenness(found.StateMachine.Value);
            }

            foreach (var state in found.States)
            {
                WriteVisuals(VisualsOf(target, state.Key), state.Value);
            }
        }

        private static AlarmStatusVisuals VisualsOf(HmiAlarmClass target, UnifiedAlarmState state)
        {
            return state switch
            {
                UnifiedAlarmState.Raised => target.RaisedState,
                UnifiedAlarmState.Acknowledged => target.AcknowledgedState,
                UnifiedAlarmState.Cleared => target.ClearedState,
                UnifiedAlarmState.AcknowledgedCleared => target.AcknowledgedClearedState,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised alarm state: {state}")
            };
        }

        private static void WriteVisuals(AlarmStatusVisuals target, UnifiedAlarmStateVisuals visuals)
        {
            if (visuals.BackColor != null)
            {
                target.BackColor = ToColor(visuals.BackColor);
            }

            if (visuals.TextColor != null)
            {
                target.TextColor = ToColor(visuals.TextColor);
            }

            if (visuals.Flashing != null)
            {
                target.Flashing = visuals.Flashing.Value;
            }
        }

        private void WriteDiscrete(HmiDiscreteAlarm target, UnifiedDiscreteAlarm alarm)
        {
            target.RaisedStateTag = _hmi.Tags.Find(alarm.Tag).Name;
            target.RaisedStateTagBitNumber = alarm.Bit;
            target.TriggerMode = alarm.Trigger switch
            {
                UnifiedAlarmTrigger.RisingEdge => HmiDiscreteAlarmTriggerMode.OnRisingEdge,
                UnifiedAlarmTrigger.FallingEdge => HmiDiscreteAlarmTriggerMode.OnFallingEdge,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised trigger: {alarm.Trigger}")
            };
            WriteCommon(target, alarm.Common);
        }

        private void WriteAnalog(HmiAnalogAlarm target, UnifiedAnalogAlarm alarm)
        {
            target.RaisedStateTag = _hmi.Tags.Find(alarm.Tag).Name;
            target.Condition = ToOpenness(alarm.Condition);
            target.ConditionValue = double.Parse(alarm.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            WriteCommon(target, alarm.Common);
        }

        private void WriteCommon(AlarmBase target, UnifiedAlarmCommon common)
        {
            target.AlarmClass = _classNames[common.AlarmClass];
            if (common.Priority != null)
            {
                target.Priority = common.Priority.Value;
            }

            WriteText(target.EventText, common.Texts.Text, common.Name);
            if (common.Texts.Info != null)
            {
                WriteText(target.InfoText, common.Texts.Info, common.Name);
            }

            if (common.Texts.Origin != null)
            {
                target.Origin = common.Texts.Origin;
            }

            if (common.Texts.Area != null)
            {
                target.Area = common.Texts.Area;
            }
        }

        private void WriteText(MultilingualText text, string value, string alarm)
        {
            var item = text.Items.Find(_language)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"Alarm '{alarm}' has no text for the project's editing language {_language.Culture.Name}");

            item.Text = UnifiedRichText.Format(value);
        }

        /// <remarks>A fourth channel is the alpha, as GetUnifiedAlarms reads a colour that is not opaque.</remarks>
        private static Color ToColor(string channels)
        {
            var parts = channels.Split(',').Select(channel => int.Parse(channel.Trim(), CultureInfo.InvariantCulture)).ToArray();

            return parts.Length == 4 ? Color.FromArgb(parts[3], parts[0], parts[1], parts[2]) : Color.FromArgb(parts[0], parts[1], parts[2]);
        }

        private static HmiAlarmStateMachine ToOpenness(UnifiedAlarmStateMachine machine)
        {
            return machine switch
            {
                UnifiedAlarmStateMachine.Raise => HmiAlarmStateMachine.Raise,
                UnifiedAlarmStateMachine.RaiseClear => HmiAlarmStateMachine.RaiseClear,
                UnifiedAlarmStateMachine.RaiseRequiresAcknowledgement => HmiAlarmStateMachine.RaiseRequiresAcknowledgement,
                UnifiedAlarmStateMachine.RaiseClearOptionalAcknowledgement => HmiAlarmStateMachine.RaiseClearOptionalAcknowledgement,
                UnifiedAlarmStateMachine.RaiseClearRequiresAcknowledgement => HmiAlarmStateMachine.RaiseClearRequiresAcknowledgement,
                UnifiedAlarmStateMachine.RaiseClearRequiresAcknowledgementAndReset => HmiAlarmStateMachine.RaiseClearRequiresAcknowledgementAndReset,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised state machine: {machine}")
            };
        }

        private static HmiAlarmCondition ToOpenness(UnifiedAlarmCondition condition)
        {
            return condition switch
            {
                UnifiedAlarmCondition.UpperLimit => HmiAlarmCondition.UpperLimit,
                UnifiedAlarmCondition.UpperLimitOrEqual => HmiAlarmCondition.UpperLimitOrEqual,
                UnifiedAlarmCondition.LowerLimit => HmiAlarmCondition.LowerLimit,
                UnifiedAlarmCondition.LowerLimitOrEqual => HmiAlarmCondition.LowerLimitOrEqual,
                UnifiedAlarmCondition.Equal => HmiAlarmCondition.Equal,
                UnifiedAlarmCondition.NotEqual => HmiAlarmCondition.NotEqual,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised condition: {condition}")
            };
        }
    }
}
