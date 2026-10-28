namespace TiaMcpServer.Siemens
{
    /// <summary>The TIA Portal process to attach to, or none, and why.</summary>
    public sealed class PortalProcessChoice
    {
        /// <summary>Records a choice.</summary>
        /// <param name="process">The process to attach to, or null to start a new one.</param>
        /// <param name="reason">Why it was chosen.</param>
        public PortalProcessChoice(PortalProcessInfo? process, PortalProcessChoiceReason reason)
        {
            Process = process;
            Reason = reason;
        }

        /// <summary>The process to attach to, or null when a new one must be started.</summary>
        public PortalProcessInfo? Process { get; }

        /// <summary>Why it was chosen.</summary>
        public PortalProcessChoiceReason Reason { get; }
    }
}
