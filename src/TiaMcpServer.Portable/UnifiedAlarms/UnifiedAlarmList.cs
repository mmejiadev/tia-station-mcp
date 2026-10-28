using System.Collections.Generic;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>WinCC Unified alarm classes and alarms to create, in that order.</summary>
    public sealed class UnifiedAlarmList
    {
        /// <summary>Creates a list.</summary>
        /// <param name="classes">Alarm classes, created first so the alarms can use them.</param>
        /// <param name="discrete">Discrete alarms.</param>
        /// <param name="analog">Analog alarms.</param>
        public UnifiedAlarmList(IReadOnlyList<UnifiedAlarmClassDescription> classes, IReadOnlyList<UnifiedDiscreteAlarm> discrete, IReadOnlyList<UnifiedAnalogAlarm> analog)
        {
            Classes = classes;
            Discrete = discrete;
            Analog = analog;
        }

        /// <summary>Alarm classes.</summary>
        public IReadOnlyList<UnifiedAlarmClassDescription> Classes { get; }

        /// <summary>Discrete alarms.</summary>
        public IReadOnlyList<UnifiedDiscreteAlarm> Discrete { get; }

        /// <summary>Analog alarms.</summary>
        public IReadOnlyList<UnifiedAnalogAlarm> Analog { get; }
    }
}
