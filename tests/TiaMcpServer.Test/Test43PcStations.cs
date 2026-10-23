using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

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

        private static readonly string[] MissingStartScreenError = { "A start screen has not been configured." };

        private static readonly string[] EncryptedTransferPasswordErrors =
        {
            "The entered password confirmation does not match the password.",
            "The entered password does not comply with the password security concept: At least one special character, at least one number, minimum length of 8 characters, one lower case and one upper case letter."
        };

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

        /// <remarks>
        /// Without a card a PC station has no interface and cannot reach the PLC (measured 2026-10-22);
        /// the class projects plug "IE general" in slot 2, which TIA Portal names <c>IE general_1</c>.
        /// </remarks>
        [TestMethod]
        [DataRow("OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2")]
        [DataRow("OrderNumber:6AV2 104-0xxxx-xxxx/17.0.0.0")]
        public void AddPcStation_MeasuredRuntime_HasANetworkInterface(string runtime)
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, runtime);

            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology().Where(node => node.DevicePath.StartsWith(StationName, System.StringComparison.Ordinal)).ToList();
            Assert.AreEqual(1, nodes.Count, "nodes: " + string.Join(", ", AssemblyHooks.SharedPortal.GetNetworkTopology().Select(node => node.DevicePath + ":" + node.InterfaceName)));
        }

        [TestMethod]
        public void AddPcStation_ThenConnectToThePlcSubnet_PutsItOnThatSubnet()
        {
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 215-1BG40-0XB0/V4.7", "PLC_1");
            var plc = AssemblyHooks.SharedPortal.GetNetworkTopology().Single(node => node.DevicePath.StartsWith("PLC_1", System.StringComparison.Ordinal));
            AssemblyHooks.SharedPortal.CreateSubnet(plc.DevicePath, plc.InterfaceName, "PN/IE_1", _testDirectory);
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPc);
            var pc = AssemblyHooks.SharedPortal.GetNetworkTopology().Single(node => node.DevicePath.StartsWith(StationName, System.StringComparison.Ordinal));

            var subnet = AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(pc.DevicePath, pc.InterfaceName, "PN/IE_1", _testDirectory);

            Assert.AreEqual("PN/IE_1", subnet);
        }

        /// <remarks>
        /// Measured 2026-10-22: with a start screen, all that is left is the encrypted-transfer password of
        /// the runtime settings, which Openness V20 does not expose anywhere; it is set once by hand in TIA
        /// Portal (Runtime settings, General). Any other error fails here.
        /// </remarks>
        [TestMethod]
        public void CompileSoftware_UnifiedPcStationWithAStartScreen_OnlyTheEncryptedTransferPasswordRemains()
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPc);
            var hmiPath = StationName + "/" + RuntimeName;
            AssemblyHooks.SharedPortal.DrawUnifiedScreen(hmiPath, UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""Main"", ""startScreen"": true, ""items"": [] }"), _testDirectory);

            var errors = CompileErrors(hmiPath);

            CollectionAssert.AreEquivalent(EncryptedTransferPasswordErrors, errors, string.Join(" | ", errors));
        }

        /// <remarks>
        /// Measured 2026-10-22: a classic runtime's start screen has no setter in Openness V20 either, so
        /// a new RT Advanced station compiles with that one error, as a new classic panel does (Test40).
        /// </remarks>
        [TestMethod]
        public void CompileSoftware_RtAdvancedPcStation_OnlyTheMissingStartScreenRemains()
        {
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.RuntimeAdvanced);

            var errors = CompileErrors(StationName + "/" + RuntimeName);

            CollectionAssert.AreEqual(MissingStartScreenError, errors, string.Join(" | ", errors));
        }

        private static string[] CompileErrors(string softwarePath)
        {
            return AssemblyHooks.SharedPortal.CompileSoftware(softwarePath).Messages
                .Where(message => message.Severity == CompilationSeverity.Error && message.Description.Length > 0 && !message.Description.StartsWith("Compiling ", System.StringComparison.Ordinal))
                .Select(message => message.Description.Trim())
                .ToArray();
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
