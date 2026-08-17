using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// The end of the loop: a program is compiled, downloaded to a virtual controller and put into
    /// RUN. This is what makes generated code testable rather than merely syntactically valid.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test11Download : IDisposable
    {
        // Taken from the project: PLC_0's PROFINET interface is configured at this address.
        private const string ControllerAddress = "192.168.0.1";
        private const string ControllerSubnetMask = "255.255.255.0";
        private const int PingTimeoutMilliseconds = 2000;
        private const int PingAttempts = 8;
        private const int IsoTcpPort = 102;
        private const int TcpTimeoutMilliseconds = 3000;
        private const int SurvivalWaitMilliseconds = 15000;

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

        /// <summary>
        /// Releases the controller handles this test held.
        /// </summary>
        /// <remarks>
        /// MSTest builds one instance of the class per test method, so this runs per test. It has
        /// to exist: the runtime now holds a live handle per controller it created, and a handle
        /// left behind keeps a virtual controller registered after the run.
        /// </remarks>
        public void Dispose()
        {
            _runtime.Dispose();
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
            // As the project's own CPU, not the unspecified controller: the text libraries are
            // tied to device identity and the hardware download succeeds either way, so this is
            // the only remaining difference between the two.
            _runtime.CreateInstance(_instanceName, "CPU1511");

            // Without this the controller reports 0.0.0.0 and TIA Portal cannot find it. The
            // address has to match the CPU's address in the project, which is 192.168.0.1.
            var addressed = _runtime.SetInstanceAddress(_instanceName, ControllerAddress, ControllerSubnetMask);
            CollectionAssert.Contains(addressed.IpAddresses.ToList(), ControllerAddress);

            // Before compiling, not after: the flag governs compilation, so blocks built without
            // it stay unsimulatable however many times they are downloaded.
            AssemblyHooks.SharedPortal.EnableSimulationSupport();

            var compilation = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);
            Assert.IsTrue(compilation.IsSuccessful, $"The program does not compile:\n{string.Join("\n", compilation.Errors)}");

            // Hardware too, and after enabling simulation support: that setting invalidates the
            // compiled hardware configuration, and downloading a stale one fails with an error
            // that blames the target rather than the project.
            var hardware = AssemblyHooks.SharedPortal.CompileHardware(Settings.Project1PlcSoftwarePath0);
            Assert.IsTrue(hardware.IsSuccessful, $"The hardware does not compile:\n{string.Join("\n", hardware.Errors)}");

            var download = AssemblyHooks.SharedPortal.DownloadToSimulation(Settings.Project1PlcSoftwarePath0);

            // Every message, not only Errors: a download can report failure while every message
            // that carries a description is an Information, and then the filtered view is empty
            // and says nothing. The network mode is included because a download that cannot
            // connect says nothing about why, and the mode is the first thing to check.
            Assert.IsTrue(
                download.IsSuccessful,
                $"Download failed (network mode {SimulationRuntime.NetworkMode}, " +
                $"severity={download.Severity}, errors={download.ErrorCount}, warnings={download.WarningCount}):\n" +
                string.Join("\n", download.Messages.Select(message => $"  [{message.Severity}] {message.Path} — {message.Description}")));

            // A controller that holds a program can finally enter RUN, which is the state a test
            // needs it in before it can drive inputs and read outputs.
            var running = _runtime.StartInstance(_instanceName);
            StringAssert.Contains(running.OperatingState, "Run", "The controller did not reach RUN after the download");
        }

        [TestMethod]
        public void DescribeSimulationConnection_AddressedInstance_AppliesTheConnection()
        {
            _runtime.CreateInstance(_instanceName);
            var instance = _runtime.SetInstanceAddress(_instanceName, ControllerAddress, ControllerSubnetMask);

            // The mode decides whether the controller is on the virtual Ethernet adapter at all:
            // over Softbus it is reachable only by PLCSIM itself, and no download can find it.
            Console.WriteLine($"network mode: {SimulationRuntime.NetworkMode}");
            Console.WriteLine($"instance: state={instance.OperatingState} cpu={instance.CpuType} licence={instance.LicenseStatus} ips=[{string.Join(", ", instance.IpAddresses)}]");

            // The scan finding nothing and the controller not being on the wire look identical
            // from Openness. Pinging separates them: no reply means PLCSIM is not reachable and
            // the fault is below TIA Portal; a reply with an empty scan means the opposite.
            Console.WriteLine($"ping: {WaitForPingReply(ControllerAddress)}");

            // ISO-TCP, the port a download actually travels over. Ping and this are different
            // paths through the firewall, so one answering says nothing about the other — and the
            // PLCSIM virtual adapter sits on a Public network profile, where Windows blocks
            // unsolicited inbound traffic by default.
            Console.WriteLine($"tcp {ControllerAddress}:{IsoTcpPort}: {DescribeTcpReach(ControllerAddress, IsoTcpPort)}");

            // The remaining suspect: an address set on a running controller is reported but may
            // only be bound to the virtual adapter when the interface comes up.
            var cycled = _runtime.PowerCycleInstance(_instanceName);

            Console.WriteLine($"after cycle: state={cycled.OperatingState} ips=[{string.Join(", ", cycled.IpAddresses)}]");
            Console.WriteLine($"ping after power cycle: {WaitForPingReply(ControllerAddress)}");

            var report = AssemblyHooks.SharedPortal.DescribeSimulationConnection(Settings.Project1PlcSoftwarePath0);

            // Printed because the point of this test is the measurement, not only the assertion:
            // what GetAccessibleDevices finds is what separates "we are passing the wrong object"
            // from "nothing is answering at all", and those need opposite fixes.
            Console.WriteLine(report);

            // IsConfigured stayed False through every attempt before ApplyConfiguration was
            // called. This is the regression test for that fix.
            StringAssert.Contains(report, "IsConfigured=True", "The connection did not apply");
        }

        /// <summary>
        /// Pings until the controller answers or the attempts run out.
        /// </summary>
        /// <remarks>
        /// A single ping straight after addressing cannot tell "unreachable" from "not on the wire
        /// yet" — the virtual adapter takes a moment once an address is assigned. Reporting which
        /// attempt succeeded is what separates the two, and a measurement that cannot tell them
        /// apart is what cost this project a day in August.
        /// </remarks>
        private static string WaitForPingReply(string address)
        {
            using var ping = new Ping();

            for (var attempt = 1; attempt <= PingAttempts; attempt++)
            {
                if (ping.Send(address, PingTimeoutMilliseconds).Status == IPStatus.Success)
                {
                    return $"answered on attempt {attempt}";
                }
            }

            return $"no reply after {PingAttempts} attempts";
        }

        /// <remarks>
        /// Not a behaviour test: a measurement that separates two candidate causes for instances
        /// disappearing on their own. If the controller is already gone before any address is
        /// assigned, the runtime is reclaiming instances nobody holds a handle to, and every
        /// method in <see cref="SimulationRuntime"/> opening and closing one is the defect. If it
        /// only dies after <c>SetInstanceAddress</c>, the address assignment is what kills it.
        /// </remarks>
        [TestMethod]
        public void CreateInstance_LeftAlone_StaysRegistered()
        {
            _runtime.CreateInstance(_instanceName);
            Console.WriteLine($"straight after create:      {DescribeRegistration()}");

            Thread.Sleep(SurvivalWaitMilliseconds);
            Console.WriteLine($"after {SurvivalWaitMilliseconds} ms idle:        {DescribeRegistration()}");

            _runtime.SetInstanceAddress(_instanceName, ControllerAddress, ControllerSubnetMask);
            Console.WriteLine($"straight after addressing:  {DescribeRegistration()}");

            Thread.Sleep(SurvivalWaitMilliseconds);
            Console.WriteLine($"after {SurvivalWaitMilliseconds} ms addressed:   {DescribeRegistration()}");

            Assert.IsTrue(
                _runtime.ListInstances().Any(instance => instance.Name == _instanceName),
                "The virtual controller unregistered itself while nothing was using it");
        }

        /// <summary>Whether the runtime still lists this test's controller, and in what state.</summary>
        private string DescribeRegistration()
        {
            var instance = _runtime.ListInstances().FirstOrDefault(candidate => candidate.Name == _instanceName);

            return instance == null
                ? "GONE"
                : $"present, state={instance.OperatingState}, ips=[{string.Join(", ", instance.IpAddresses)}]";
        }

        /// <summary>Reports whether the controller accepts a TCP connection on a port.</summary>
        /// <remarks>
        /// A download travels over ISO-TCP on port 102. Reporting the outcome rather than
        /// asserting it is deliberate: this is a measurement of the machine, not of the code.
        /// </remarks>
        private static string DescribeTcpReach(string address, int port)
        {
            using var client = new TcpClient();

            if (!client.ConnectAsync(address, port).Wait(TcpTimeoutMilliseconds))
            {
                return "timed out";
            }

            return client.Connected ? "connected" : "refused";
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
