using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// PC stations carrying an HMI runtime, built as TIA Portal V20 accepts them.
    /// </summary>
    /// <remarks>
    /// The runtimes are the two the class uses (2025-26 corpus): WinCC Unified PC RT, as in
    /// <c>0967 - unified_00</c>, and WinCC RT Advanced. Measured 2026-10-20: the runtime is a module of
    /// the station's rack, plugged with an empty name, and TIA Portal calls it <c>HMI_RT_1</c>.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test43PcStations
    {
        private const string StationName = "PC-System_1";
        private const string RuntimeName = "HMI_RT_1";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "PcStations");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        [DataRow("OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2")]
        [DataRow("OrderNumber:6AV2 104-0xxxx-xxxx/17.0.0.0")]
        public void AddPcStation_MeasuredRuntime_ItsSoftwareIsTheHmi(string runtime)
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, runtime);

            var paths = AssemblyHooks.SharedPortal.GetSoftwarePaths(StationName);
            Assert.IsTrue(
                paths.Any(path => path.EndsWith("/" + RuntimeName, System.StringComparison.Ordinal)),
                "software paths: " + string.Join(", ", paths));
        }

        [TestMethod]
        public void AddPcStation_RuntimeNotMeasured_ThrowsInvalidParamsAndAddsNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.AddPcStation(StationName, "OrderNumber:6AV2 124-0JC01-0AX0/17.0.0.0"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.IsFalse(AssemblyHooks.SharedPortal.GetDevices().Any(), "a refused station was left in the project");
        }

        /// <remarks>
        /// The generic plug path names the module, which a PC station's rack refuses, and a failed plug
        /// followed by another once took TIA Portal down; the refusal comes before Openness is asked.
        /// </remarks>
        [TestMethod]
        public void PlugModule_IntoPcStationRack_ThrowsInvalidParams()
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPc);
            var module = new ModuleToPlug(PcHmiRuntime.RuntimeAdvanced, "HMI_RT_2", 2);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.PlugModule(StationName + "/" + RuntimeName, module, _testDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void AddPcStation_NameTaken_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, "OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.AddPcStation(StationName, "OrderNumber:6AV2 104-0xxxx-xxxx/17.0.0.0"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }
    }
}
