using Siemens.Engineering.HmiUnified.HmiTags;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedAlarms;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Finds every reason an alarm list cannot be created on an HMI as it stands.</summary>
    /// <remarks>
    /// Measured 2026-10-25 on TIA Portal V20: Openness takes a trigger tag that does not exist, a tag of a
    /// type the alarm does not take, an alarm class that does not exist, a tag and bit already raising
    /// another alarm, and an analog value outside its tag's type, and the compile refuses each of them.
    /// So all of them are found here, against the HMI's own tags and alarms, before anything is created.
    /// </remarks>
    internal sealed class UnifiedAlarmChecker
    {
        private readonly UnifiedHmiSoftware _hmi;
        private readonly HashSet<string> _classes;
        private readonly HashSet<string> _discreteNames;
        private readonly HashSet<string> _analogNames;
        private readonly Dictionary<string, string> _raisingBits;

        /// <summary>Creates a checker for one HMI, reading its classes and alarms once.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <remarks>Each Openness read crosses into TIA Portal, so the HMI's alarms are read here once, not per alarm checked.</remarks>
        public UnifiedAlarmChecker(UnifiedHmiSoftware hmi)
        {
            _hmi = hmi;
            _classes = new HashSet<string>(hmi.AlarmClasses.Select(found => found.Name), StringComparer.OrdinalIgnoreCase);
            _analogNames = new HashSet<string>(hmi.AnalogAlarms.Select(alarm => alarm.Name), StringComparer.OrdinalIgnoreCase);
            _discreteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _raisingBits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var alarm in hmi.DiscreteAlarms)
            {
                _discreteNames.Add(alarm.Name);
                _raisingBits[BitKey(alarm.RaisedStateTag, alarm.RaisedStateTagBitNumber)] = alarm.Name;
            }
        }

        /// <summary>The names of the HMI's alarm classes, system ones included, as TIA Portal spells them.</summary>
        public IReadOnlyCollection<string> ClassNames => _classes;

        /// <summary>Every problem the list has on this HMI.</summary>
        /// <param name="list">The classes and alarms.</param>
        /// <returns>The problems, empty when there is none.</returns>
        public List<string> Problems(UnifiedAlarmList list)
        {
            var problems = list.Classes.Where(found => _classes.Contains(found.Name)).Select(found => $"alarm class '{found.Name}' already exists").ToList();
            problems.AddRange(list.Discrete.Where(alarm => _discreteNames.Contains(alarm.Common.Name)).Select(alarm => $"a discrete alarm named '{alarm.Common.Name}' already exists"));
            problems.AddRange(list.Analog.Where(alarm => _analogNames.Contains(alarm.Common.Name)).Select(alarm => $"an analog alarm named '{alarm.Common.Name}' already exists"));

            var classes = new HashSet<string>(list.Classes.Select(found => found.Name), StringComparer.OrdinalIgnoreCase);
            problems.AddRange(list.Discrete.Select(alarm => alarm.Common).Concat(list.Analog.Select(alarm => alarm.Common))
                .Where(common => !classes.Contains(common.AlarmClass) && !_classes.Contains(common.AlarmClass))
                .Select(common => $"'{common.Name}' uses alarm class '{common.AlarmClass}', which does not exist"));

            problems.AddRange(list.Discrete.Select(DiscreteProblem).OfType<string>());
            problems.AddRange(list.Analog.Select(AnalogProblem).OfType<string>());

            return problems;
        }

        private string? DiscreteProblem(UnifiedDiscreteAlarm alarm)
        {
            var tag = FindTag(alarm.Tag);
            if (tag == null)
            {
                return $"'{alarm.Common.Name}' is raised by tag '{alarm.Tag}', which does not exist";
            }

            var type = UnifiedAlarmTriggerTypes.DiscreteProblem(tag.DataType, alarm.Bit);
            if (type != null)
            {
                return $"'{alarm.Common.Name}': {type}";
            }

            return _raisingBits.TryGetValue(BitKey(tag.Name, alarm.Bit), out var taken)
                ? $"'{alarm.Common.Name}': bit {alarm.Bit} of '{tag.Name}' already raises alarm '{taken}'"
                : null;
        }

        private string? AnalogProblem(UnifiedAnalogAlarm alarm)
        {
            var tag = FindTag(alarm.Tag);
            if (tag == null)
            {
                return $"'{alarm.Common.Name}' compares tag '{alarm.Tag}', which does not exist";
            }

            var type = UnifiedAlarmTriggerTypes.AnalogProblem(tag.DataType);
            if (type != null)
            {
                return $"'{alarm.Common.Name}': {type}";
            }

            return UnifiedAlarmTriggerTypes.AnalogValueProblem(tag.DataType, alarm.Value, $"'{alarm.Common.Name}', the value");
        }

        private static string BitKey(string tag, uint bit)
        {
            return $"{tag}|{bit}";
        }

        private HmiTag? FindTag(string name)
        {
            return _hmi.Tags.Find(name);
        }

    }
}
