using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// A test bench built from scratch rather than inherited.
    ///
    /// <c>TestProject1</c> came with the upstream fork and carries settings nobody here chose:
    /// its CPU sits at access level "No access (complete protection)", user management is on, and
    /// Openness V20 exposes none of that, so it can neither be read nor changed from code. While
    /// the download fails, an inherited setting and a defect in this server look identical.
    ///
    /// So this class builds the smallest project that can accept a download — one CPU, nothing
    /// else — and drives the same sequence against it. If the download succeeds here and fails
    /// against <c>TestProject1</c>, the difference is the fixture and not the code, and that is a
    /// conclusion rather than another hypothesis.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test15MinimalFixture : IDisposable
    {
        // Read from TestProject1's own CPU, not guessed: OrderNumber 6ES7 511-1AL03-0AB0, V4.0,
        // which TIA names "CPU 1511-1 PN". Building the fixture around the same CPU keeps the
        // comparison honest — only the project settings differ.
        private const string CpuTypeIdentifier = "OrderNumber:6ES7 511-1AL03-0AB0/V4.0";
        private const string CpuName = "PLC_1";

        // CreateWithItem names the station and the CPU inside it, and here both are "PLC_1", so
        // the device item sits one level down. The software resolves by the bare name; the device
        // item needs the full path, which is the distinction CLAUDE.md means by "always full
        // paths". Taken from what GetNetworkTopology actually reported, not assumed.
        private const string CpuDeviceItemPath = "PLC_1/PLC_1";
        private const string ControllerSubnetMask = "255.255.255.0";

        private SimulationRuntime _runtime = new SimulationRuntime();
        private string _instanceName = string.Empty;
        private string _projectDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _runtime = new SimulationRuntime();
            _instanceName = "TiaMcpMinimal_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _projectDirectory = AssemblyHooks.CreateTestDirectory();

            // Must happen in this process and before any instance exists.
            SimulationRuntime.UseTcpIpNetworkMode();
        }

        public void Dispose()
        {
            _runtime.Dispose();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();

            try
            {
                _runtime.DeleteInstance(_instanceName);
            }
            catch (PortalException)
            {
                // The instance may never have been created. Losing the real failure behind a
                // cleanup error would waste the run.
            }
        }

        [TestMethod]
        public void CreateProject_WithOneCpu_ReportsItsAddress()
        {
            AssemblyHooks.SharedPortal.CreateProject(_projectDirectory, "MinimalFixture");

            var created = AssemblyHooks.SharedPortal.AddDevice(CpuTypeIdentifier, CpuName);

            var topology = AssemblyHooks.SharedPortal.GetNetworkTopology();

            foreach (var node in topology)
            {
                Console.WriteLine($"{node.DevicePath} / {node.InterfaceName} {node.NetworkType} {node.Address} subnet='{node.SubnetName}'");
            }

            Assert.IsTrue(
                topology.Any(node => node.DevicePath.StartsWith(created, StringComparison.Ordinal)),
                $"The new CPU '{created}' has no network interface in the topology");
        }

        [TestMethod]
        [Ignore("A project created from scratch cannot compile its hardware in V20: it demands a " +
                "password for confidential PLC configuration data, and Openness exposes nothing to " +
                "set one — no type or member matching 'Confidential' exists in the V20 API. Kept " +
                "rather than deleted because a deleted test stops recording that the gap is there. " +
                "Unignore once the password can be set, or once the fixture is built another way.")]
        public void DownloadToSimulation_MinimalProject_ReachesRun()
        {
            AssemblyHooks.SharedPortal.CreateProject(_projectDirectory, "MinimalFixture");
            AssemblyHooks.SharedPortal.AddDevice(CpuTypeIdentifier, CpuName);

            var address = ResolveControllerAddress();
            Console.WriteLine($"CPU address in the project: {address}");

            _runtime.CreateInstance(_instanceName);
            _runtime.SetInstanceAddress(_instanceName, address, ControllerSubnetMask);

            // Before compiling: the flag governs compilation, so blocks built without it stay
            // unsimulatable however many times they are downloaded.
            AssemblyHooks.SharedPortal.EnableSimulationSupport();

            var compilation = AssemblyHooks.SharedPortal.CompileSoftware(CpuName);
            Assert.IsTrue(compilation.IsSuccessful, $"The empty project does not compile:\n{string.Join("\n", compilation.Errors)}");

            var hardware = AssemblyHooks.SharedPortal.CompileHardware(CpuDeviceItemPath);
            Assert.IsTrue(hardware.IsSuccessful, "The hardware does not compile:\n" + Describe(hardware));

            Console.WriteLine(AssemblyHooks.SharedPortal.DescribeSimulationConnection(CpuDeviceItemPath));

            var download = AssemblyHooks.SharedPortal.DownloadToSimulation(CpuDeviceItemPath);

            Assert.IsTrue(
                download.IsSuccessful,
                $"Download failed (network mode {SimulationRuntime.NetworkMode}, " +
                $"severity={download.Severity}, errors={download.ErrorCount}):\n" +
                string.Join("\n", download.Messages.Select(message => $"  [{message.Severity}] {message.Path} — {message.Description}")));

            var running = _runtime.StartInstance(_instanceName);
            StringAssert.Contains(running.OperatingState, "Run", "The controller did not reach RUN after the download");
        }

        /// <summary>Every message a report carries, not the filtered view.</summary>
        /// <remarks>
        /// <c>Errors</c> keeps only error-severity messages that carry a description, which is
        /// right for feeding a fix loop and wrong as the only thing a failing assertion prints:
        /// when the failure does not fit that filter the message is blank and says nothing. That
        /// happened twice in one night before this helper existed.
        /// </remarks>
        private static string Describe(CompilationReport report)
        {
            return $"severity={report.Severity}, errors={report.ErrorCount}, warnings={report.WarningCount}\n" +
                string.Join("\n", report.Messages.Select(message => $"  [{message.Severity}] {message.Path} — {message.Description}"));
        }

        /// <summary>The address TIA gave the new CPU, rather than one assumed for it.</summary>
        private static string ResolveControllerAddress()
        {
            var node = AssemblyHooks.SharedPortal.GetNetworkTopology()
                .FirstOrDefault(candidate => !string.IsNullOrEmpty(candidate.Address))
                ?? throw new InvalidOperationException("The new CPU has no addressed interface");

            return node.Address;
        }
    }
}
