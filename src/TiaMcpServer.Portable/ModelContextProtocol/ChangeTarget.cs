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
    /// These families, and nothing else:
    /// <list type="bullet">
    /// <item><description><c>PLC_0/Blocks/FB_Station</c> — a place in the project tree.</description></item>
    /// <item><description><c>simulation/Station_1</c> — a virtual controller, which is not in the project.</description></item>
    /// <item><description><c>simulation-runtime</c> — the PLCSIM Advanced runtime itself, machine-wide.</description></item>
    /// <item><description><c>project</c> — the project as a whole: saving it, closing it, copying it.</description></item>
    /// <item><description><c>opcua/192.168.0.1:4840</c> — an OPC UA server on the network, which a session may read from.</description></item>
    /// <item><description><c>network/RZ616 Wi-Fi 6E 160MHz</c> — a PC network interface a session may search for devices through.</description></item>
    /// <item><description><c>git/Cell</c> — the Git repository in a VCI workspace's folder: branches, commits, patches.</description></item>
    /// <item><description><c>git-remote/Cell</c> — that repository's remote, which a pull contacts.</description></item>
    /// <item><description><c>library</c> and <c>library/FP_Motor</c> — the project library, and one type in it.</description></item>
    /// <item><description><c>global-library/C:/Libraries/Faceplates</c> — a global library by its folder, which is outside the project.</description></item>
    /// <item><description><c>master-copy-delete/Motors/FB_Motor</c> — deleting a master copy or a master copy folder of the project library.</description></item>
    /// <item><description><c>secret/Plc_Cell</c> — a stored secret a write puts into the project, by its name and never its value.</description></item>
    /// </list>
    /// </remarks>
    public static partial class ChangeTarget
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

        /// <summary>
        /// The PLCSIM Advanced runtime itself, as opposed to a controller inside it.
        /// </summary>
        /// <remarks>
        /// Its own name, and not under the <c>simulation/</c> prefix, because it is a different
        /// authority: the runtime's network mode is machine-wide and affects every PLCSIM user on
        /// that machine, while a controller affects only itself. A policy that let a session create
        /// controllers should not thereby let it reconfigure the runtime those controllers — and
        /// everybody else's — live in.
        ///
        /// It follows that a policy allowing <c>simulation/*</c> does **not** allow this. That is
        /// the intent, and a session that needs it names it.
        /// </remarks>
        public const string SimulationRuntime = "simulation-runtime";

        /// <summary>
        /// The project library as a whole: types copied into it from a global library.
        /// </summary>
        /// <remarks>
        /// Not a place under a PLC or an HMI: a type in the project library belongs to no device, and
        /// one instance of it can sit on every HMI of the project. A policy that allows <c>library</c>
        /// lets a session add types; releasing one is <see cref="LibraryType"/>, a rule of its own.
        /// </remarks>
        public const string ProjectLibrary = "library";

        /// <summary>
        /// Deleting from the project library whatever nothing uses, every type at once.
        /// </summary>
        /// <remarks>
        /// Its own name, not <see cref="ProjectLibrary"/>: a policy allowing <c>library</c> lets a session add types, and
        /// must not thereby let it delete versions and whole types in bulk. Like <see cref="SimulationRuntime"/>, a session
        /// that needs it names it.
        /// </remarks>
        public const string ProjectLibraryCleanUp = "library-cleanup";

        /// <summary>
        /// Installing a WinCC Unified Custom Web Control in the project: a web page that runs in every HMI runtime of it.
        /// </summary>
        /// <remarks>
        /// Its own name, not <see cref="Project"/>: a policy that lets a session save the project - which deleting library
        /// types asks for - must not thereby let it put script into the HMI, and in the server's own TIA Portal close and
        /// open the project.
        /// </remarks>
        public const string CustomWebControls = "custom-web-controls";

        /// <summary>Deleting from the project library whole types nothing uses, besides their unused versions.</summary>
        /// <remarks>Apart from <see cref="ProjectLibraryCleanUp"/>: a rule allowing old versions to be pruned must not thereby let whole types go.</remarks>
        public const string ProjectLibraryCleanUpTypes = ProjectLibraryCleanUp + "/types";

        private const string SimulationPrefix = "simulation/";
        private const string LibraryPrefix = ProjectLibrary + "/";
        private const string OpcUaPrefix = "opcua/";
        private const string NetworkPrefix = "network/";
        private const string GitPrefix = "git/";
        private const string GitRemotePrefix = "git-remote/";
        private const string SecretPrefix = "secret/";

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

        /// <summary>One type in the project library.</summary>
        /// <param name="typeName">The type's name, e.g. <c>FP_Motor</c>.</param>
        /// <returns>The target name, for example <c>library/FP_Motor</c>.</returns>
        /// <remarks>
        /// Releasing a type changes what every new instance of it shows, on every HMI, so it is named
        /// by the type rather than by a screen. The prefix keeps a rule about a PLC that happens to be
        /// called like a type from governing the type. It does not keep apart a device literally named
        /// <c>library</c>, whose program targets would share the prefix; <c>simulation/</c> and
        /// <c>opcua/</c> live with the same limit, as no character is measured to be refused in a device name.
        /// </remarks>
        public static string LibraryType(string typeName)
        {
            return LibraryPrefix + (typeName ?? string.Empty).Trim('/');
        }

        /// <summary>Installing one Custom Web Control, by its name.</summary>
        /// <param name="controlName">The control's name, e.g. <c>TiaStation.PilotLight</c>.</param>
        /// <returns>The target name, for example <c>custom-web-controls/TiaStation.PilotLight</c>.</returns>
        /// <remarks>
        /// By name, so a rule can allow the catalogue's controls (<c>custom-web-controls/TiaStation.*</c>) without allowing
        /// every definition a session writes, nor the replacing of a control already installed under another name.
        /// </remarks>
        public static string CustomWebControl(string controlName)
        {
            return CustomWebControls + "/" + (controlName ?? string.Empty).Trim('/');
        }

        /// <summary>An OPC UA server, by host and port.</summary>
        /// <param name="host">The machine, as it appears in the endpoint URL.</param>
        /// <param name="port">The TCP port.</param>
        /// <returns>The target name, for example <c>opcua/192.168.0.1:4840</c>.</returns>
        /// <remarks>
        /// Not a change, and still a target. Reading a running machine alters nothing on it, but it
        /// is contact with a machine, and a mistyped address reaches whatever answers there. So the
        /// endpoint has to be listed in the same policy the writes are listed in. The prefix keeps a
        /// rule about the project, such as <c>PLC_0/*</c>, from being read as a rule about the network.
        /// </remarks>
        public static string OpcUaServer(string host, int port)
        {
            return OpcUaPrefix + (host ?? string.Empty) + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A network interface of this PC, by the name TIA Portal gives it.</summary>
        /// <param name="pcInterfaceName">The interface, e.g. <c>RZ616 Wi-Fi 6E 160MHz</c>.</param>
        /// <returns>The target name, for example <c>network/RZ616 Wi-Fi 6E 160MHz</c>.</returns>
        /// <remarks>
        /// Not a change, and still a target, for the reason an OPC UA server is one: searching for accessible devices
        /// sends a discovery request to everything on that network, which in a workshop or a plant is other people's
        /// machines. So the interface has to be listed in the policy before a session may search through it. The
        /// prefix keeps a rule about the project from being read as a rule about the network.
        /// </remarks>
        public static string NetworkInterface(string pcInterfaceName)
        {
            return NetworkPrefix + (pcInterfaceName ?? string.Empty).Trim();
        }

        /// <summary>The Git repository in a VCI workspace's folder, by the workspace's name.</summary>
        /// <param name="workspaceName">The workspace, e.g. <c>Cell</c>.</param>
        /// <returns>The target name, for example <c>git/Cell</c>.</returns>
        /// <remarks>
        /// Outside the project, and what a commit there records is somebody's history: a policy names the repositories a
        /// session may commit to, branch in and import into, one by one.
        /// </remarks>
        public static string GitRepository(string workspaceName)
        {
            return GitPrefix + (workspaceName ?? string.Empty).Trim();
        }

        /// <summary>The remote of that repository, which a pull contacts over the network.</summary>
        /// <param name="workspaceName">The workspace, e.g. <c>Cell</c>.</param>
        /// <returns>The target name, for example <c>git-remote/Cell</c>.</returns>
        /// <remarks>
        /// Its own prefix rather than under <c>git/</c>: working in a repository and reaching a server are different
        /// permissions, and a policy that allows <c>git/*</c> does not thereby let a session contact one.
        /// </remarks>
        public static string GitRemote(string workspaceName)
        {
            return GitRemotePrefix + (workspaceName ?? string.Empty).Trim();
        }

        /// <summary>A stored secret that a write puts into the project, by its name.</summary>
        /// <param name="secretName">The secret's name, as <c>ListSecrets</c> gives it, e.g. <c>Plc_Cell</c>.</param>
        /// <returns>The target name, for example <c>secret/Plc_Cell</c>.</returns>
        /// <remarks>
        /// A target of its own, and not covered by the project's: the store holds every password a person has kept on this
        /// machine, and a policy that lets a session build devices must not thereby let it protect them with any of those
        /// it names - one kept for the workshop's PLCs among them. So each secret a session may use is listed. The store
        /// compares names case aside, and so do rules, so <c>secret/plc_cell</c> is the same target.
        /// </remarks>
        public static string Secret(string secretName)
        {
            return SecretPrefix + (secretName ?? string.Empty).Trim();
        }
    }
}
