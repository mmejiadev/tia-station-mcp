namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>What a WinCC Unified discrete and analog alarm have in common: name, class, priority and texts.</summary>
    public sealed class UnifiedAlarmCommon
    {
        /// <summary>The class a new alarm gets in TIA Portal V20 when none is set (measured 2026-10-25).</summary>
        public const string DefaultClass = "Alarm";

        /// <summary>Creates the common part.</summary>
        /// <param name="name">The alarm's name, unique among alarms of its kind.</param>
        /// <param name="alarmClass">Its alarm class, a system one or one of the project's.</param>
        /// <param name="priority">Its priority from 0 to 16, or null for TIA Portal's.</param>
        /// <param name="texts">What the operator reads.</param>
        public UnifiedAlarmCommon(string name, string alarmClass, byte? priority, UnifiedAlarmTexts texts)
        {
            Name = name;
            AlarmClass = alarmClass;
            Priority = priority;
            Texts = texts;
        }

        /// <summary>The alarm's name.</summary>
        public string Name { get; }

        /// <summary>Its alarm class.</summary>
        public string AlarmClass { get; }

        /// <summary>Its priority, or null.</summary>
        public byte? Priority { get; }

        /// <summary>What the operator reads.</summary>
        public UnifiedAlarmTexts Texts { get; }
    }
}
