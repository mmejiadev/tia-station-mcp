using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Owns the one TIA Portal the whole test assembly shares, and the temp directory tests write
    /// into.
    /// </summary>
    /// <remarks>
    /// Starting a portal per test class is not deterministic. A portal does not die the moment it
    /// is disposed, so the next class to connect attaches to the previous, still-closing process
    /// instead of starting its own. An attached portal deliberately refuses to close the open
    /// project, because that project belongs to whoever started it, and the project directory then
    /// stays locked. The result was tests that passed alone and failed in a full run, plus the odd
    /// orphaned process holding the licence.
    ///
    /// So the assembly starts exactly one portal and owns it. Every test attaches to that one and
    /// releases only its own handle. Ownership stays here, which is also the only place that can
    /// safely close the project and delete the working directory.
    /// </remarks>
    [TestClass]
    public static class AssemblyHooks
    {
        private static Portal? _sharedPortal;
        private static ServiceProvider? _services;

        /// <summary>The portal shared by every test in the assembly.</summary>
        public static Portal SharedPortal =>
            _sharedPortal ?? throw new InvalidOperationException("AssemblyInitialize has not run");

        /// <summary>Root directory tests write into. Removed once the shared portal is released.</summary>
        public static string WorkingRoot { get; private set; } = string.Empty;

        /// <summary>
        /// Full path of the sample project on disk, retrieved once from the archive that ships in
        /// <c>assets/</c>. This is what replaced the absolute <c>D:\Siemens\...</c> paths the
        /// inherited tests were pinned to: the fixture now builds itself on any machine.
        /// </summary>
        public static string ProjectPath { get; private set; } = string.Empty;

        /// <summary>Starts the shared TIA Portal.</summary>
        /// <param name="context">Supplied by MSTest.</param>
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            Openness.Initialize();

            // Fail immediately rather than attach to someone else's session. ConnectPortal joins a
            // running TIA Portal instead of starting one, so a suite launched while TIA is open by
            // hand shares that project and any dialog waiting for a human. One run blocked for
            // thirteen hours that way, and a hang gives no output to diagnose.
            var running = Portal.GetRunningPortalCount();
            if (running > 0)
            {
                Assert.Inconclusive(
                    $"{running} TIA Portal process(es) already running. Close TIA Portal before running the suite: " +
                    "the tests would attach to that session instead of starting their own.");
            }

            WorkingRoot = Path.Combine(Path.GetTempPath(), "TiaMcpServer.Test", Guid.NewGuid().ToString("N"));

            _sharedPortal = new Portal();
            _sharedPortal.ConnectPortal();

            // Retrieved once for the whole run. Tests that need the project open reopen it by
            // path; tests that mutate it (SaveAs, import) work inside the temp copy, so nothing
            // they do can affect the archive in the repository.
            ProjectPath = _sharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(WorkingRoot, "project"));

            _services = BuildServices(Settings.Project1PolicyPath);

            McpServer.SetServiceProvider(_services);
        }

        /// <summary>
        /// Builds the container the MCP tools resolve everything from, governance included.
        /// </summary>
        /// <param name="policyPath">The write policy this run is governed by.</param>
        /// <returns>The provider.</returns>
        /// <remarks>
        /// The suite wires itself the way <c>Program</c> does rather than setting
        /// <c>McpServer.Portal</c> and hoping the rest resolves: the write tools now go through the
        /// governance layer, and a suite that bypassed it would be testing a server nobody runs.
        ///
        /// The audit trail is written under the working root, so a run leaves its record where the
        /// rest of its leftovers go and the repository's own trail is never touched.
        /// </remarks>
        public static ServiceProvider BuildServices(string policyPath)
        {
            var services = new ServiceCollection();

            // The shared portal, not a new one: every test attaches to the single TIA Portal this
            // class owns, and a second Portal would start a second process.
            services.AddSingleton(_ => SharedPortal);
            services.AddSingleton<SimulationRuntime>();

            Program.RegisterGovernance(services, new CliOptions
            {
                PolicyPath = policyPath,
                AuditPath = Path.Combine(WorkingRoot, "audit.jsonl"),
                BackupRoot = Path.Combine(WorkingRoot, "backups")
            });

            return services.BuildServiceProvider();
        }

        /// <summary>Releases the shared TIA Portal and deletes the working directory.</summary>
        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // The container owns the simulation runtime, whose open handles are what keep a virtual
            // controller registered, so it goes first. It also disposes the shared portal.
            _services?.Dispose();
            _services = null;

            // Disposing closes the open project first, which is what releases the file handles
            // TIA Portal keeps inside the project directory. Deleting before this always fails.
            // Idempotent, so doing it again after the container is harmless and says out loud that
            // this class owns the portal whether or not a container was ever built.
            _sharedPortal?.Dispose();
            _sharedPortal = null;

            TryDeleteWorkingRoot();
        }

        private static void TryDeleteWorkingRoot()
        {
            if (!Directory.Exists(WorkingRoot))
            {
                return;
            }

            try
            {
                Directory.Delete(WorkingRoot, recursive: true);
            }
            catch (IOException)
            {
                // If a TIA Portal was already running when the run started, AssemblyInit attached
                // to it rather than starting one, so we never owned it and cannot close its
                // project — which leaves its directory locked. That is the correct behaviour
                // towards someone else's portal, and not worth failing an otherwise green run
                // over. %TEMP% is the right place for the leftovers to be reaped.
            }
        }

        /// <summary>Creates a fresh directory for one test under <see cref="WorkingRoot"/>.</summary>
        /// <returns>The directory path. It is not created until something writes to it.</returns>
        public static string CreateTestDirectory()
        {
            return Path.Combine(WorkingRoot, Guid.NewGuid().ToString("N"));
        }
    }
}
