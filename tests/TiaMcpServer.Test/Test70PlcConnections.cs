using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Configured connections between two PLCs, made through Openness on TIA Portal V21.
    /// </summary>
    /// <remarks>
    /// Built as measured on 2026-11-15: a CPU 1511-1 PN V4.0 and a CPU 1215C V4.7 on one subnet, 192.168.0.1 and
    /// 192.168.0.2, both opened with the class recipe so they compile.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test70PlcConnections
    {
        private const string Cpu1511 = "OrderNumber:6ES7 511-1AL03-0AB0/V4.0";
        private const string Cpu1215 = "OrderNumber:6ES7 215-1AG40-0XB0/V4.7";
        private const string Plc1500 = "PLC_A";
        private const string Plc1200 = "PLC_B";
        private const string SubnetName = "PN/IE_1";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            if (!Portal.CanUsePlcConnections)
            {
                Assert.Inconclusive("Connections between PLCs need a TIA Portal V21 build");
            }

            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "PlcConnections");
            AssemblyHooks.SharedPortal.AddDevice(Cpu1511, Plc1500);
            AssemblyHooks.SharedPortal.AddDevice(Cpu1215, Plc1200);
            var first = NodeOf(Plc1500);
            AssemblyHooks.SharedPortal.CreateSubnet(first.DevicePath, first.InterfaceName, SubnetName, Backup());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (!Portal.CanUsePlcConnections)
            {
                return;
            }

            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreatePlcConnection_S7_TiaPortalGivesTheFirstIdAtBothEnds()
        {
            JoinTheSecondPlc();

            var connection = AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup());

            Assert.AreEqual("S7Connection", connection.Kind);
            StringAssert.StartsWith(connection.Name, "S7_Connection_");
            Assert.AreEqual("100", connection.Ends.Local.ConnectionId);
            Assert.AreEqual("100", connection.Ends.Partner.ConnectionId);
            Assert.IsTrue(connection.IsValid);
        }

        [TestMethod]
        public void CreatePlcConnection_S7_TheFirstPlcEstablishesItToTheSecondsAddress()
        {
            JoinTheSecondPlc();

            var connection = AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup());

            Assert.AreEqual(Plc1500, connection.Ends.Local.Device);
            Assert.AreEqual(Plc1200, connection.Ends.Partner.Device);
            Assert.AreEqual("192.168.0.2", connection.Ends.Partner.Address);
            Assert.IsTrue(connection.Ends.IsLocalActive);
        }

        [TestMethod]
        public void CreatePlcConnection_Tcp_IsAddressedByPort()
        {
            JoinTheSecondPlc();

            var connection = AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.Tcp, Backup());

            Assert.AreEqual("TcpConnection", connection.Kind);
            StringAssert.StartsWith(connection.Ends.Local.Addressing, "port ");
        }

        [TestMethod]
        public void CreatePlcConnection_IsoOnTcp_IsAddressedByTsap()
        {
            JoinTheSecondPlc();

            var connection = AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.IsoOnTcp, Backup());

            Assert.AreEqual("IsoOnTcpConnection", connection.Kind);
            StringAssert.StartsWith(connection.Ends.Local.Addressing, "TSAP ");
        }

        [TestMethod]
        public void CreatePlcConnection_TwoBetweenTheSamePlcs_EachGetsItsOwnId()
        {
            JoinTheSecondPlc();

            var first = AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup());
            var second = AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup());

            Assert.AreNotEqual(first.Ends.Local.ConnectionId, second.Ends.Local.ConnectionId);
        }

        [TestMethod]
        public void GetPlcConnections_FromThePartner_ShowsTheEndsSwapped()
        {
            JoinTheSecondPlc();
            AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup());

            var connection = AssemblyHooks.SharedPortal.GetPlcConnections(Plc1200).Single();

            Assert.AreEqual(Plc1200, connection.Ends.Local.Device);
            Assert.AreEqual(Plc1500, connection.Ends.Partner.Device);
        }

        [TestMethod]
        public void GetPlcConnections_NoneMade_IsEmpty()
        {
            var connections = AssemblyHooks.SharedPortal.GetPlcConnections(Plc1500);

            Assert.AreEqual(0, connections.Count);
        }

        [TestMethod]
        public void CreatePlcConnection_SamePlcAtBothEnds_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1500, PlcConnectionKind.S7, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void CreatePlcConnection_PartnerOnNoSubnet_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void CreatePlcConnection_PathIsNotAPlc_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, "NoSuchPlc", PlcConnectionKind.S7, Backup()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        [TestMethod]
        public void CreatePlcConnection_OnOneSubnet_RecordsTheNetworkFirst()
        {
            JoinTheSecondPlc();
            var backup = Backup();

            AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, backup);

            Assert.IsTrue(Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Any(), "nothing was recorded in " + backup);
        }

        /// <remarks>The connection is configured at both ends, so both stations compile it; measured 0 errors on 2026-11-15.</remarks>
        [TestMethod]
        public void CreatePlcConnection_ThenCompileBothStations_NoErrors()
        {
            JoinTheSecondPlc();
            AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice(Plc1500, Backup());
            AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice(Plc1200, Backup());
            AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.S7, Backup());
            AssemblyHooks.SharedPortal.CreatePlcConnection(Plc1500, Plc1200, PlcConnectionKind.Tcp, Backup());

            var first = AssemblyHooks.SharedPortal.CompileHardware(Plc1500 + "/" + Plc1500);
            var second = AssemblyHooks.SharedPortal.CompileHardware(Plc1200 + "/" + Plc1200);

            Assert.AreEqual(0, first.ErrorCount, string.Join(" | ", first.Errors.Select(message => message.Description)));
            Assert.AreEqual(0, second.ErrorCount, string.Join(" | ", second.Errors.Select(message => message.Description)));
        }

        private void JoinTheSecondPlc()
        {
            var second = NodeOf(Plc1200);
            AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(second.DevicePath, second.InterfaceName, SubnetName, Backup());
            AssemblyHooks.SharedPortal.SetNodeAddress(second.DevicePath, second.InterfaceName, "192.168.0.2", Backup());
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
