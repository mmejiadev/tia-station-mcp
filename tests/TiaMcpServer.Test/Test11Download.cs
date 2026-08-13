using System;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// The end of the loop: a program is compiled, downloaded to a virtual controller and put into
    /// RUN. This is what makes generated code testable rather than merely syntactically valid.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test11Download
    {
        // Taken from the project: PLC_0's PROFINET interface is configured at this address.
        private const string ControllerAddress = "192.168.0.1";
        private const string ControllerSubnetMask = "255.255.255.0";

        private SimulationRuntime _runtime = new SimulationRuntime();
        private string _instanceName = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _runtime = new SimulationRuntime();
            _instanceName = "TiaMcpDownload_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Must happen in this process and before any instance exists. Setting it from a
            // separate script reads back as applied and then has no effect here.
            SimulationRuntime.UseTcpIpNetworkMode();

            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Order matters: close the project before removing the controller, so TIA Portal is
            // not holding a connection to something that is disappearing underneath it.
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
        public void DownloadToSimulation_CompiledProgram_ReachesRun()
        {
            _runtime.CreateInstance(_instanceName);

            // Without this the controller reports 0.0.0.0 and TIA Portal cannot find it. The
            // address has to match the CPU's address in the project, which is 192.168.0.1.
            var addressed = _runtime.SetInstanceAddress(_instanceName, ControllerAddress, ControllerSubnetMask);
            CollectionAssert.Contains(addressed.IpAddresses.ToList(), ControllerAddress);

            var compilation = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);
            Assert.IsTrue(compilation.IsSuccessful, $"The program does not compile:\n{string.Join("\n", compilation.Errors)}");

            var download = AssemblyHooks.SharedPortal.DownloadToSimulation(Settings.Project1PlcSoftwarePath0);

            // The network mode is reported in the failure message on purpose: a download that
            // cannot connect says nothing about why, and the mode is the first thing to check.
            Assert.IsTrue(
                download.IsSuccessful,
                $"Download failed (network mode {SimulationRuntime.NetworkMode}):\n{string.Join("\n", download.Errors)}");

            // A controller that holds a program can finally enter RUN, which is the state a test
            // needs it in before it can drive inputs and read outputs.
            var running = _runtime.StartInstance(_instanceName);
            StringAssert.Contains(running.OperatingState, "Run", "The controller did not reach RUN after the download");
        }

        [TestMethod]
        public void GetSimulationTargetName_AlwaysResolvesToAPlcSimInterface()
        {
            // The safety property, checked without performing a download. This machine offers a
            // real network adapter alongside the PLCSIM one, so a resolver that simply took the
            // first interface would target hardware. Naming what would be used is also the only
            // way to verify this without side effects.
            var target = AssemblyHooks.SharedPortal.GetSimulationTargetName(Settings.Project1PlcSoftwarePath0);

            StringAssert.Contains(
                target.ToUpperInvariant(),
                "PLCSIM",
                $"A download would go through '{target}', which is not a simulation interface");
        }

        [TestMethod]
        public void DownloadToSimulation_UnknownPath_ThrowsNotFound()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.DownloadToSimulation("NoSuchDevice"));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }
    }
}
