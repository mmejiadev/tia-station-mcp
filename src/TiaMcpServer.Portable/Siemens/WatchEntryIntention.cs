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
    ///
    /// Measured 2026-11-13 on V21: a value can be there with the flag down — the flag is read-only through
    /// Openness, so a row written by this server holds its value with the box unticked. So a value is
    /// <see cref="IsSet"/> whenever there is one, and <see cref="IsTicked"/> says whether the box is.
    /// </remarks>
    public sealed class WatchEntryIntention
    {
        /// <summary>A row that only watches its address.</summary>
        public static WatchEntryIntention None { get; } = new WatchEntryIntention(false, string.Empty, string.Empty);

        /// <summary>Creates an intention.</summary>
        /// <param name="isSet">Whether a value is prepared at all.</param>
        /// <param name="value">The value, as TIA Portal spells it for the row's display format.</param>
        /// <param name="trigger">When it would be applied, or empty when the table has no choice.</param>
        /// <param name="isTicked">Whether the row's box is ticked; when left out, a prepared value is taken as ticked.</param>
        public WatchEntryIntention(bool isSet, string value, string trigger, bool? isTicked = null)
        {
            IsSet = isSet;
            Value = value;
            Trigger = trigger;
            IsTicked = isTicked ?? isSet;
        }

        /// <summary>Whether a value is prepared at all.</summary>
        public bool IsSet { get; }

        /// <summary>The prepared value.</summary>
        public string Value { get; }

        /// <summary>When it would be applied.</summary>
        public string Trigger { get; }

        /// <summary>Whether the row's box is ticked, so that activating the table online applies the value.</summary>
        public bool IsTicked { get; }

        /// <summary>The intention as one field of a line.</summary>
        public string Line => !IsSet ? "watch only" : $"{Value}{TriggerSuffix()}{(IsTicked ? string.Empty : " (box not ticked)")}";

        private string TriggerSuffix()
        {
            return string.IsNullOrEmpty(Trigger) ? string.Empty : $" @ {Trigger}";
        }
    }
}
