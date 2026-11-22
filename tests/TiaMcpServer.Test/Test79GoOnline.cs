using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Going online with a CPU in PLCSIM Advanced and offline again, through <c>GoOnline</c> and <c>GoOffline</c>.</summary>
    /// <remarks>
    /// On a copy of the fixture, its CPU moved to an address of its own so that a PLCSIM instance of a person's, at the
    /// fixture's address, is left alone. Measured 2026-11-22 on V21: <c>GoOnline</c> with the online connection's legacy
    /// communication on, its default, killed TIA Portal or was refused; with it off it went online every time.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test79GoOnline : IDisposable
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string ControllerAddress = "192.168.0.7";
        private const string SubnetMask = "255.255.255.0";
        private const string ExportedBlock = "1_Tests/FC_Block_1";

        private SimulationRuntime _runtime = new SimulationRuntime();
        private string _instance = string.Empty;
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _runtime = new SimulationRuntime();
            _instance = "TiaMcpOnline_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            SimulationRuntime.UseTcpIpNetworkMode();

            var portal = AssemblyHooks.SharedPortal;
            portal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
            var node = portal.GetNetworkTopology().First(candidate => candidate.DevicePath.StartsWith(Software, StringComparison.Ordinal));
            portal.SetNodeAddress(node.DevicePath, node.InterfaceName, ControllerAddress, Backup());

            _runtime.CreateInstance(_instance, "CPU1511");
            _runtime.SetInstanceAddress(_instance, ControllerAddress, SubnetMask);
            portal.EnableSimulationSupport();
            Assert.IsTrue(portal.CompileSoftware(Software).IsSuccessful, "the program compiles");
            Assert.IsTrue(portal.CompileHardware(Software).IsSuccessful, "the hardware compiles");
            Assert.IsTrue(portal.DownloadToSimulation(Software).IsSuccessful, "the download reaches the instance");
            _runtime.StartInstance(_instance);
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
                _runtime.DeleteInstance(_instance);
            }
            catch (PortalException)
            {
                // The instance may never have been created; losing the real failure behind a cleanup error would waste the run.
            }
        }

        [TestMethod]
        public void GoOnline_AfterADownloadToPlcsim_GoesOnlineAndOfflineAgain()
        {
            var online = AssemblyHooks.SharedPortal.GoOnline(Software);
            Assert.AreEqual("Online", online);
            Assert.AreEqual("Online", AssemblyHooks.SharedPortal.GetOnlineState(Software));

            Assert.AreEqual("Offline", AssemblyHooks.SharedPortal.GoOffline(Software));
        }

        [TestMethod]
        public void GoOnline_WhenAlreadyOnline_AnswersOnlineAndDoesNotFail()
        {
            AssemblyHooks.SharedPortal.GoOnline(Software);

            Assert.AreEqual("Online", AssemblyHooks.SharedPortal.GoOnline(Software));
        }

        /// <remarks>Online, an export is refused; offline again it works. It is what the tool description promises.</remarks>
        [TestMethod]
        public void GoOnline_ThenAnExport_IsRefusedUntilOffline()
        {
            AssemblyHooks.SharedPortal.GoOnline(Software);
            var refused = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.ExportBlock(Software, ExportedBlock, _testDirectory));
            StringAssert.Contains(refused.Message, "online", StringComparison.OrdinalIgnoreCase);

            AssemblyHooks.SharedPortal.GoOffline(Software);

            Assert.IsNotNull(AssemblyHooks.SharedPortal.ExportBlock(Software, ExportedBlock, _testDirectory));
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
