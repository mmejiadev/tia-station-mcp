using System;
using System.IO;
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

            WorkingRoot = Path.Combine(Path.GetTempPath(), "TiaMcpServer.Test", Guid.NewGuid().ToString("N"));

            _sharedPortal = new Portal();
            _sharedPortal.ConnectPortal();

            // Retrieved once for the whole run. Tests that need the project open reopen it by
            // path; tests that mutate it (SaveAs, import) work inside the temp copy, so nothing
            // they do can affect the archive in the repository.
            ProjectPath = _sharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(WorkingRoot, "project"));
        }

        /// <summary>Releases the shared TIA Portal and deletes the working directory.</summary>
        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // Disposing closes the open project first, which is what releases the file handles
            // TIA Portal keeps inside the project directory. Deleting before this always fails.
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
