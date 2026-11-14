using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The integrated connection between a WinCC Unified HMI and a PLC, made through Openness on TIA Portal V21.
    /// </summary>
    /// <remarks>
    /// Built as the class builds it: a CPU 1512C and a PC station with WinCC Unified PC RT V21 on one subnet,
    /// 192.168.0.1 and 192.168.0.2. Measured 2026-11-11 by a probe in the same shape.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test66HmiConnections
    {
        private const string Cpu1512 = "OrderNumber:6ES7 512-1CK01-0AB0/V2.9";
        private const string PlcName = "PLC_1";
        private const string StationName = "PC-System_1";
        private const string HmiPath = StationName + "/HMI_RT_1";
        private const string SubnetName = "PN/IE_1";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "HmiConnections");
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName);
            AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice(PlcName, Backup());
            var plc = NodeOf(PlcName);
            AssemblyHooks.SharedPortal.CreateSubnet(plc.DevicePath, plc.InterfaceName, SubnetName, Backup());
            AssemblyHooks.SharedPortal.AddPcStation(StationName, PcHmiRuntime.UnifiedPcV21);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateHmiConnection_OnOneSubnet_TiaPortalNamesItAndGivesThePlcAddress()
        {
            JoinTheHmiToTheSubnet();

            var connection = AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, PlcName, Backup());

            StringAssert.StartsWith(connection.Name, "HMI_Connection_");
            Assert.AreEqual("192.168.0.1", connection.PartnerAddress);
        }

        /// <remarks>
        /// The connection is what the HMI compiles with; a broken one shows as an error under Connections. The two
        /// encrypted-transfer password errors and the missing start screen are a new runtime's own (Test43).
        /// </remarks>
        [TestMethod]
        public void CreateHmiConnection_ThenCompileTheHmi_NoErrorIsAboutTheConnection()
        {
            JoinTheHmiToTheSubnet();
            AssemblyHooks.SharedPortal.CompileHardware(PlcName + "/" + PlcName);

            AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, PlcName, Backup());

            var errors = AssemblyHooks.SharedPortal.CompileSoftware(HmiPath).Errors.Select(message => message.Path + " " + message.Description).ToList();
            Assert.IsFalse(errors.Any(error => error.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0), string.Join(" | ", errors));
        }

        [TestMethod]
        public void CreateHmiConnection_Twice_ThrowsInvalidState()
        {
            JoinTheHmiToTheSubnet();
            AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, PlcName, Backup());

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, PlcName, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void CreateHmiConnection_HmiOnNoSubnet_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, PlcName, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void CreateHmiConnection_PlcPathIsTheHmi_ThrowsNotFound()
        {
            JoinTheHmiToTheSubnet();

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, HmiPath, Backup()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        [TestMethod]
        public void CreateHmiConnection_OnOneSubnet_RecordsTheNetworkFirst()
        {
            JoinTheHmiToTheSubnet();
            var backup = Backup();

            AssemblyHooks.SharedPortal.CreateHmiConnection(HmiPath, PlcName, backup);

            Assert.IsTrue(Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Any(), "nothing was recorded in " + backup);
        }

        private void JoinTheHmiToTheSubnet()
        {
            var pc = NodeOf(StationName);
            AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(pc.DevicePath, pc.InterfaceName, SubnetName, Backup());
            AssemblyHooks.SharedPortal.SetNodeAddress(pc.DevicePath, pc.InterfaceName, "192.168.0.2", Backup());
        }

        private static NetworkNodeInfo NodeOf(string device)
        {
            return AssemblyHooks.SharedPortal.GetNetworkTopology().Single(node => node.DevicePath.StartsWith(device, StringComparison.Ordinal));
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
