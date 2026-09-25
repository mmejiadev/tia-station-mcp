namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What a row is set to do to the address beyond watching it, and when.
    /// </summary>
    /// <remarks>
    /// The word is Openness' own: <c>ModifyIntention</c> and <c>ForceIntention</c> are flags on a
    /// row saying that a value is *prepared*, not that anything has happened. Nothing in a project
    /// file reaches a machine; the value takes effect when somebody opens the table on a connected
    /// CPU and activates it.
    ///
    /// That distinction is the reason this is its own type rather than two more strings on a row.
    /// A row with <see cref="IsSet"/> false is a row that only watches, and a caller reading a
    /// table has to be able to see which is which at a glance.
    /// </remarks>
    public sealed class WatchEntryIntention
    {
        /// <summary>A row that only watches its address.</summary>
        public static WatchEntryIntention None { get; } = new WatchEntryIntention(false, string.Empty, string.Empty);

        /// <summary>Creates an intention.</summary>
        /// <param name="isSet">Whether a value is prepared at all.</param>
        /// <param name="value">The value, as TIA Portal spells it for the row's display format.</param>
        /// <param name="trigger">When it would be applied, or empty when the table has no choice.</param>
        public WatchEntryIntention(bool isSet, string value, string trigger)
        {
            IsSet = isSet;
            Value = value;
            Trigger = trigger;
        }

        /// <summary>Whether a value is prepared at all.</summary>
        public bool IsSet { get; }

        /// <summary>The prepared value.</summary>
        public string Value { get; }

        /// <summary>When it would be applied.</summary>
        public string Trigger { get; }

        /// <summary>The intention as one field of a line.</summary>
        public string Line => IsSet ? $"{Value}{TriggerSuffix()}" : "watch only";

        private string TriggerSuffix()
        {
            return string.IsNullOrEmpty(Trigger) ? string.Empty : $" @ {Trigger}";
        }
    }
}
