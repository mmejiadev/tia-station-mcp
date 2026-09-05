using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Every tool that writes goes through the governance layer, and none of them writes when the
    /// policy says no.
    /// </summary>
    /// <remarks>
    /// The rule this class exists for is the one that cannot be checked by reading: **a write tool
    /// that forgot to ask the guard would still pass every other test in the suite**, because those
    /// tests run under a policy that allows what they do. Here the policy allows nothing, so a tool
    /// that writes anyway is a tool that never asked.
    ///
    /// It needs no TIA Portal work: a refused change never reaches the Openness API, which is the
    /// property being asserted. It lives in this assembly only because calling the MCP tools loads
    /// types that reference Openness — the governance project must stay free of that.
    ///
    /// Adding a write tool without adding it here leaves the gap this class was written to close.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test16GuardedWrites
    {
        // The assembly runs tests in parallel at method level, and this class swaps the container
        // every MCP tool resolves from. Overlapping with another class would refuse that class's
        // writes for reasons it could never explain, so this one runs on its own.

        private const string SomeDirectory = "C:/nowhere";

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            // A policy file that does not exist. Not an empty one: "there is no policy" is the
            // state a machine is really in before anyone configures it, and it must deny.
            var missingPolicy = Path.Combine(AssemblyHooks.WorkingRoot, "no-such-policy.json");

            McpServer.SetServiceProvider(BuildDenyEverythingServices(missingPolicy));
        }

        // EndOfClass is load-bearing, not decoration. MSTest runs [ClassCleanup] at the end of the
        // **assembly** by default, so without it this class installed a container that refuses
        // everything and never took it back before the next class ran. Nothing failed, because every
        // other test in the suite called Portal directly and never touched the container — until
        // Test17WritesApplied did, and all three of its allowed-path tests failed at once.
        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static void ClassCleanup()
        {
            // Back to the suite's own container, or every test after this one would be refused.
            McpServer.SetServiceProvider(AssemblyHooks.BuildServices(Settings.Project1PolicyPath));
        }

        [TestMethod]
        public void WriteScl_WithNoPolicy_IsRefusedAndWritesNothing()
        {
            var response = McpServer.WriteScl(Settings.Project1PlcSoftwarePath0, "FUNCTION_BLOCK \"FB_X\"");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
            Assert.AreEqual(0, response.GeneratedBlocks.Count);
        }

        [TestMethod]
        public void CompileSoftware_WithNoPolicy_IsRefusedAndCompilesNothing()
        {
            // A compile is guarded because it is what makes code downloadable, so an ungoverned
            // session must not be able to run one. The error count stays at zero: a refusal is not
            // a build that found no errors, and the caller must not read it as one.
            var response = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
            Assert.AreEqual(0, response.ErrorCount);
        }

        [TestMethod]
        public void CompileSoftwareAsJob_WithNoPolicy_FailsTheJobRatherThanReportingSuccess()
        {
            // The defect this asserts: a refusal is an ordinary response, so a job that only watched
            // for exceptions went to Succeeded while nothing had been compiled. A job has nothing but
            // its state to speak with, and Succeeded would have said the work happened.
            var accepted = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0, string.Empty, runAsJob: true);
            var jobId = accepted.Meta?["jobId"]?.GetValue<string>();

            Assert.IsFalse(string.IsNullOrEmpty(jobId), "runAsJob must return a job id to poll");

            var job = WaitUntilFinished(jobId!);

            Assert.AreEqual("Failed", job.State);
            StringAssert.Contains(job.Detail, "no policy is configured", StringComparison.Ordinal);
        }

        [TestMethod]
        public void ImportBlock_WithNoPolicy_IsRefused()
        {
            var response = McpServer.ImportBlock(Settings.Project1PlcSoftwarePath0, "1_Tests", SomeDirectory + "/FC.xml");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void ImportType_WithNoPolicy_IsRefused()
        {
            var response = McpServer.ImportType(Settings.Project1PlcSoftwarePath0, "Common", SomeDirectory + "/UDT.xml");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void ImportFromDocuments_WithNoPolicy_IsRefused()
        {
            var response = McpServer.ImportFromDocuments(Settings.Project1PlcSoftwarePath0, string.Empty, SomeDirectory, "FC_Block_1");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ImportBlocksFromDocuments_WithNoPolicy_IsRefused()
        {
            // No server and no request context, so the notification path is not exercised: what is
            // under test is that the import never starts. A RequestContext cannot be built here —
            // its constructor rejects a null server — and building one is not the point.
            var response = await McpServer.ImportBlocksFromDocuments(
                null!,
                null!,
                Settings.Project1PlcSoftwarePath0,
                string.Empty,
                SomeDirectory);

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void CreateIoSystem_WithNoPolicy_IsRefused()
        {
            var response = McpServer.CreateIoSystem(Settings.Project1PlcSoftwarePath0, "Cell_IO");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void AssignDeviceToIoSystem_WithNoPolicy_IsRefused()
        {
            var response = McpServer.AssignDeviceToIoSystem(Settings.Project1PlcSoftwarePath0, "Cell_IO");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        /// <remarks>
        /// The rule CLAUDE.md states and this class exists to enforce: a tool that changes anything
        /// goes through the guard. Setting an address rewires which machine answers where, so it is
        /// a write like any other and a policy that says nothing about the target refuses it.
        /// </remarks>
        [TestMethod]
        public void SetDeviceAddress_WithNoPolicy_IsRefused()
        {
            var response = McpServer.SetDeviceAddress(Settings.Project1PlcSoftwarePath0, "PROFINET interface_1", "192.168.0.42");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void DownloadToSimulation_WithNoPolicy_IsRefused()
        {
            var response = McpServer.DownloadToSimulation(Settings.Project1PlcSoftwarePath0);

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
            Assert.AreEqual(0, response.ErrorCount);
        }

        [TestMethod]
        public void CreateSimulationInstance_WithNoPolicy_IsRefused()
        {
            var response = McpServer.CreateSimulationInstance("TiaMcpRefused", "192.168.0.1");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void StartSimulationInstance_WithNoPolicy_IsRefused()
        {
            var response = McpServer.StartSimulationInstance("TiaMcpRefused");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void StopSimulationInstance_WithNoPolicy_IsRefused()
        {
            var response = McpServer.StopSimulationInstance("TiaMcpRefused");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void UseTcpIpNetworkMode_WithNoPolicy_IsRefused()
        {
            // Machine-wide, and that is why it is guarded and why it has its own target: it changes
            // the runtime every PLCSIM user on this computer shares, not one controller.
            var response = McpServer.UseTcpIpNetworkMode();

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void CompileHardware_WithNoPolicy_IsRefusedAndCompilesNothing()
        {
            // Guarded for the same reason CompileSoftware is: it is what makes a configuration
            // downloadable. The error count stays at zero because a refusal is not a compile that
            // found nothing wrong — nothing was compiled at all.
            var response = McpServer.CompileHardware(Settings.Project1PlcSoftwarePath0);

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
            Assert.AreEqual(0, response.ErrorCount);
        }

        [TestMethod]
        public void EnableSimulationSupport_WithNoPolicy_IsRefused()
        {
            // Without this setting no program can run on a virtual controller, and with it every
            // program can. That makes it a precondition for a download rather than a diagnostic,
            // so an ungoverned session must not be able to turn it on.
            var response = McpServer.EnableSimulationSupport();

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void WriteSimulationTag_WithNoPolicy_IsRefusedAndReportsNoValue()
        {
            // Driving an input on a controller is a change to what a machine is doing, so it asks
            // permission like every other write. The value stays null: a refused Bool write that
            // reported false would read as the tag holding false.
            var response = McpServer.WriteSimulationTag("TiaMcpRefused", "DB_Cell.CellStart", "true");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
            Assert.IsNull(response.Value);
        }

        [TestMethod]
        public void DeleteSimulationInstance_WithNoPolicy_IsRefused()
        {
            var response = McpServer.DeleteSimulationInstance("TiaMcpRefused");

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void SaveProject_WithNoPolicy_IsRefused()
        {
            var response = McpServer.SaveProject();

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void SaveAsProject_WithNoPolicy_IsRefused()
        {
            var response = McpServer.SaveAsProject(Path.Combine(AssemblyHooks.WorkingRoot, "refused-copy"));

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        [TestMethod]
        public void CloseProject_WithNoPolicy_IsRefused()
        {
            var response = McpServer.CloseProject();

            AssertRefused(response.Message, response.Meta?["outcome"]?.GetValue<string>());
        }

        /// <summary>Polls a job until it stops, or fails the test.</summary>
        /// <remarks>
        /// The one place in the repository that waits on wall-clock time, and only because the real
        /// dispatcher is the thread pool: what is under test here is the wiring, not the store's
        /// logic, which <c>JobStoreTests</c> asserts without waiting at all. A refusal never reaches
        /// Openness, so this returns in milliseconds; the generous bound is there so a loaded machine
        /// reports a timeout with a reason instead of a flake.
        /// </remarks>
        private static ResponseJob WaitUntilFinished(string jobId)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                var job = McpServer.GetJobStatus(jobId);

                if (job.Meta?["isFinished"]?.GetValue<bool>() == true)
                {
                    return job;
                }

                System.Threading.Thread.Sleep(25);
            }

            Assert.Fail($"job '{jobId}' had not finished after 30 s");

            throw new InvalidOperationException("unreachable");
        }

        private static void AssertRefused(string? message, string? outcome)
        {
            Assert.AreEqual("Refused", outcome, $"the change was not refused: {message}");
            StringAssert.Contains(message ?? string.Empty, "no policy is configured", StringComparison.Ordinal);
        }

        private static ServiceProvider BuildDenyEverythingServices(string policyPath)
        {
            var services = new ServiceCollection();

            // The shared portal, because a tool may read the project's state before it proposes a
            // change. Nothing here is disposed on purpose: this container borrows the portal that
            // AssemblyHooks owns, and disposing it would close the project the rest of the suite
            // is still using.
            services.AddSingleton(_ => AssemblyHooks.SharedPortal);
            services.AddSingleton<SimulationRuntime>();

            Program.RegisterGovernance(services, new CliOptions
            {
                PolicyPath = policyPath,
                AuditPath = Path.Combine(AssemblyHooks.WorkingRoot, "refused-audit.jsonl"),

                // Under the working root, never the repository's own .tia-mcp/backups: a refused
                // change still allocates a backup directory, because the location is decided before
                // the policy is consulted so the audit line can name it. Those directories are the
                // empty ones ListBackups reports, and here they are temporary.
                BackupRoot = Path.Combine(AssemblyHooks.WorkingRoot, "refused-backups")
            });

            return services.BuildServiceProvider();
        }
    }
}
