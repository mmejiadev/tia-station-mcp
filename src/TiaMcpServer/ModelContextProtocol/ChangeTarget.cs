namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// The names a write policy is written against.
    /// </summary>
    /// <remarks>
    /// A policy file is edited by a person deciding what a session may touch, so the names it
    /// contains have to be predictable. They are built here, in one place, rather than interpolated
    /// at sixteen call sites where a stray slash would silently put a target outside every rule —
    /// and an unmatched target is refused, so the mistake would show up as an inexplicable refusal
    /// rather than as a typo.
    ///
    /// Three families, and nothing else:
    /// <list type="bullet">
    /// <item><description><c>PLC_0/Blocks/FB_Station</c> — a place in the project tree.</description></item>
    /// <item><description><c>simulation/Station_1</c> — a virtual controller, which is not in the project.</description></item>
    /// <item><description><c>project</c> — the project as a whole: saving it, closing it, copying it.</description></item>
    /// </list>
    /// </remarks>
    public static class ChangeTarget
    {
        /// <summary>
        /// The project as a whole.
        /// </summary>
        /// <remarks>
        /// One name for save, save-as and close, because what they have in common is what a policy
        /// author cares about: they act on the project rather than on anything inside it. Where a
        /// copy would be written is recorded as the change's value, which is audited but not
        /// matched against rules.
        /// </remarks>
        public const string Project = "project";

        private const string SimulationPrefix = "simulation/";

        /// <summary>A place in the project tree.</summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>PLC_0</c>.</param>
        /// <param name="groupPath">Group within the program, or empty for its root.</param>
        /// <returns>The target name.</returns>
        public static string Program(string softwarePath, string groupPath = "")
        {
            var software = (softwarePath ?? string.Empty).Trim('/');
            var group = (groupPath ?? string.Empty).Trim('/');

            return group.Length == 0 ? software : $"{software}/{group}";
        }

        /// <summary>A virtual controller in the PLCSIM Advanced runtime.</summary>
        /// <param name="instanceName">The controller's name.</param>
        /// <returns>The target name.</returns>
        /// <remarks>
        /// Prefixed rather than bare: a controller lives in the runtime, not in the project, and a
        /// policy that could not tell the two apart would let a rule about a PLC named
        /// <c>Station_1</c> govern a simulation instance that happens to share the name.
        /// </remarks>
        public static string Simulation(string instanceName)
        {
            return SimulationPrefix + (instanceName ?? string.Empty).Trim('/');
        }
    }
}
