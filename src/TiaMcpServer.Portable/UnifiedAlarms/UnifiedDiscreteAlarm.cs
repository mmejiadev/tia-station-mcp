namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>A WinCC Unified discrete alarm: raised by one bit of an HMI tag.</summary>
    /// <remarks>See <see cref="UnifiedAlarmTriggerTypes"/> for the tags it takes.</remarks>
    public sealed class UnifiedDiscreteAlarm
    {
        /// <summary>Creates a discrete alarm.</summary>
        /// <param name="common">Name, class, priority and texts.</param>
        /// <param name="tag">The HMI tag holding the bit.</param>
        /// <param name="bit">The bit, from 0.</param>
        /// <param name="trigger">The edge that raises it.</param>
        public UnifiedDiscreteAlarm(UnifiedAlarmCommon common, string tag, uint bit, UnifiedAlarmTrigger trigger)
        {
            Common = common;
            Tag = tag;
            Bit = bit;
            Trigger = trigger;
        }

        /// <summary>Name, class, priority and texts.</summary>
        public UnifiedAlarmCommon Common { get; }

        /// <summary>The HMI tag holding the bit.</summary>
        public string Tag { get; }

        /// <summary>The bit.</summary>
        public uint Bit { get; }

        /// <summary>The edge that raises it.</summary>
        public UnifiedAlarmTrigger Trigger { get; }
    }
}
