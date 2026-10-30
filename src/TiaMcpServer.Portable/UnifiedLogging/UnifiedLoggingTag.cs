namespace TiaMcpServer.UnifiedLogging
{
    /// <summary>A WinCC Unified logging tag: which HMI tag is logged, into which data log, and when.</summary>
    public sealed class UnifiedLoggingTag
    {
        /// <summary>Describes a logging tag.</summary>
        /// <param name="tag">The HMI tag logged.</param>
        /// <param name="name">The logging tag's name, unique among the logging tags of that HMI tag.</param>
        /// <param name="dataLog">The data log it writes to.</param>
        /// <param name="timing">When it writes, and the cycle and its factor when it writes cyclically.</param>
        public UnifiedLoggingTag(string tag, string name, string dataLog, (UnifiedLoggingMode Mode, string? Cycle, uint? CycleFactor) timing)
        {
            Tag = tag;
            Name = name;
            DataLog = dataLog;
            Mode = timing.Mode;
            Cycle = timing.Cycle;
            CycleFactor = timing.CycleFactor;
        }

        /// <summary>The HMI tag logged.</summary>
        public string Tag { get; }

        /// <summary>The logging tag's name.</summary>
        public string Name { get; }

        /// <summary>The data log it writes to.</summary>
        public string DataLog { get; }

        /// <summary>When it writes.</summary>
        public UnifiedLoggingMode Mode { get; }

        /// <summary>The cycle, e.g. <c>T1s</c>, for a cyclic logging tag; null otherwise.</summary>
        public string? Cycle { get; }

        /// <summary>How many cycles go between two writes, e.g. 5 with <c>T1s</c> for every 5 s; null to leave TIA Portal's.</summary>
        public uint? CycleFactor { get; }
    }
}
