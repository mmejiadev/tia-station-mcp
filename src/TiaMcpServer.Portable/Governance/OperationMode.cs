namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What this session is allowed to act on.
    /// </summary>
    /// <remarks>
    /// The distinction the whole governance layer exists for. It is not a preference or a
    /// convenience: the worst case in one mode is a failed simulation, and in the other it is
    /// injury to a person or destruction of expensive equipment.
    ///
    /// A session holds exactly one mode for its lifetime. There is no switching, because a mode
    /// that can change under a running operation is a mode nobody can reason about.
    /// </remarks>
    public enum OperationMode
    {
        /// <summary>
        /// PLCSIM Advanced only. The default, and where the work happens.
        /// </summary>
        /// <remarks>
        /// Generate, compile, test, iterate, break things. Writes are still planned and audited —
        /// there is only ever one execution path — but whitelisted operations confirm themselves,
        /// so the loop stays fast.
        /// </remarks>
        Study = 0,

        /// <summary>
        /// Physical hardware. Exceptional, never the default, and unreachable in the default build.
        /// </summary>
        /// <remarks>
        /// Every write needs human confirmation, one action at a time. The whitelist denies by
        /// default with no wildcards, and a failed audit write refuses the action rather than
        /// letting it through.
        ///
        /// Only to be used with a teacher or workshop supervisor physically present and with
        /// access to the emergency stop. No software enforces that; it is a requirement of the
        /// project and it is stated wherever this mode is mentioned.
        /// </remarks>
        Workshop = 1
    }
}
