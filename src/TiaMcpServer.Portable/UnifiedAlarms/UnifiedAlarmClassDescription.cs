using System.Collections.Generic;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>A WinCC Unified alarm class to create.</summary>
    /// <remarks>
    /// Measured 2026-10-25 on TIA Portal V20: the 18 system classes (Alarm, Warning, Information…) can be
    /// neither renamed nor deleted; a class name is unique in any case; a priority runs from 0 to 16; a
    /// new class raises in red, flashing, until told otherwise.
    /// </remarks>
    public sealed class UnifiedAlarmClassDescription
    {
        /// <summary>Creates a description.</summary>
        /// <param name="name">The class's name.</param>
        /// <param name="priority">Its priority from 0 to 16, or null for TIA Portal's 0.</param>
        /// <param name="stateMachine">The states its alarms go through, or null for TIA Portal's default.</param>
        /// <param name="states">The colours of the states given; the others keep TIA Portal's.</param>
        public UnifiedAlarmClassDescription(string name, byte? priority, UnifiedAlarmStateMachine? stateMachine, IReadOnlyDictionary<UnifiedAlarmState, UnifiedAlarmStateVisuals> states)
        {
            Name = name;
            Priority = priority;
            StateMachine = stateMachine;
            States = states;
        }

        /// <summary>The class's name.</summary>
        public string Name { get; }

        /// <summary>Its priority, or null.</summary>
        public byte? Priority { get; }

        /// <summary>Its state machine, or null.</summary>
        public UnifiedAlarmStateMachine? StateMachine { get; }

        /// <summary>The colours of the states given.</summary>
        public IReadOnlyDictionary<UnifiedAlarmState, UnifiedAlarmStateVisuals> States { get; }
    }
}
