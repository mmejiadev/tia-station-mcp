namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>A WinCC Unified analog alarm: raised when a number tag compares with a value.</summary>
    /// <remarks>See <see cref="UnifiedAlarmTriggerTypes"/> for the tags it takes.</remarks>
    public sealed class UnifiedAnalogAlarm
    {
        /// <summary>Creates an analog alarm.</summary>
        /// <param name="common">Name, class, priority and texts.</param>
        /// <param name="tag">The HMI number tag compared.</param>
        /// <param name="condition">How it is compared.</param>
        /// <param name="value">The value, as written; checked against the tag's type once the tag is known.</param>
        public UnifiedAnalogAlarm(UnifiedAlarmCommon common, string tag, UnifiedAlarmCondition condition, string value)
        {
            Common = common;
            Tag = tag;
            Condition = condition;
            Value = value;
        }

        /// <summary>Name, class, priority and texts.</summary>
        public UnifiedAlarmCommon Common { get; }

        /// <summary>The HMI tag compared.</summary>
        public string Tag { get; }

        /// <summary>How it is compared.</summary>
        public UnifiedAlarmCondition Condition { get; }

        /// <summary>The value, as written.</summary>
        public string Value { get; }
    }
}
