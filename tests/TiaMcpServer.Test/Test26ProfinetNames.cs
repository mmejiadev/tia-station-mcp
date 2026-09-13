using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The PROFINET device name: the name a controller resolves before it talks IP, read from the
    /// topology and written back onto a node.
    /// </summary>
    /// <remarks>
    /// Like Test24Subnets these tests change the network, so each works on a copy retrieved per
    /// test rather than on the shared fixture.
    ///
    /// <see cref="GetNetworkTopology_ThePlcProfinetInterface_CarriesADeviceName"/> is the one that
    /// measures rather than asserts a rule of ours: it is what proves the attribute this server
    /// reads is the attribute TIA Portal keeps the name in. The whole feature rests on that name,
    /// and nothing in the compiler would notice if it were spelt wrong -- the read would simply
    /// return nothing, for every node, for ever.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test26ProfinetNames
    {
        // The PROFIBUS communications card of the fixture: an interface that cannot have a
        // PROFINET name, which is what makes it the right subject for the refusal test.
        private const string ProfibusCardPath = "PC-System_0/CP 5622_1";

        private const string SomeValidName = "cell-plc-1";

        private string _testDirectory = string.Empty;
        private string _backupDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            _backupDirectory = Path.Combine(_testDirectory, "backup");

            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>
        /// TIA generates a device name for every PROFINET interface, so a fixture PLC has one
        /// before anybody sets it. An empty column here means the attribute is not the one TIA
        /// stores the name in, not that the project has no names.
        /// </remarks>
        [TestMethod]
        public void GetNetworkTopology_ThePlcProfinetInterface_CarriesADeviceName()
        {
            var plc = ProfinetPlcNode();

            Assert.AreNotEqual(
                string.Empty,
                plc.ProfinetDeviceName,
                $"{plc.DevicePath} / {plc.InterfaceName} is a PROFINET interface with no device name");
        }

        /// <remarks>
        /// Two columns from the read tool aim the write, and the read tool is where the result
        /// shows up. The same round trip Test24Subnets asserts for subnets, for the same reason.
        /// </remarks>
        [TestMethod]
        public void SetProfinetDeviceName_ANameFromTheTopology_IsWhatTheTopologyThenPrints()
        {
            var plc = ProfinetPlcNode();

            var stored = AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                plc.DevicePath, plc.InterfaceName, SomeValidName, _backupDirectory);

            Assert.AreEqual(SomeValidName, stored);
            Assert.AreEqual(SomeValidName, NodeAt(plc.DevicePath, plc.InterfaceName).ProfinetDeviceName);
        }

        /// <remarks>
        /// Idempotence, which every write in this repository owes its callers: a retry after a
        /// timeout must not be the thing that breaks the project.
        /// </remarks>
        [TestMethod]
        public void SetProfinetDeviceName_TheSameNameTwice_KeepsIt()
        {
            var plc = ProfinetPlcNode();

            AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                plc.DevicePath, plc.InterfaceName, SomeValidName, _backupDirectory);

            var stored = AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                plc.DevicePath, plc.InterfaceName, SomeValidName, _backupDirectory);

            Assert.AreEqual(SomeValidName, stored);
        }

        [TestMethod]
        public void SetProfinetDeviceName_RecordsTheNetworkBeforeChangingIt()
        {
            var plc = ProfinetPlcNode();

            AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                plc.DevicePath, plc.InterfaceName, SomeValidName, _backupDirectory);

            Assert.IsTrue(
                File.Exists(Path.Combine(_backupDirectory, "network", "topology.txt")),
                $"No network backup was written to {_backupDirectory}");
        }

        /// <remarks>
        /// A PROFIBUS node has no PROFINET name and never will. Being told which attributes it
        /// does have is what turns "not a PROFINET node" into something actionable: the PROFINET
        /// interface of that station is a different node, and its name is in the same table.
        /// </remarks>
        [TestMethod]
        public void SetProfinetDeviceName_AProfibusNode_ThrowsInvalidState()
        {
            var card = ProfibusNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                    card.DevicePath, card.InterfaceName, SomeValidName, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "It has:", StringComparison.Ordinal);
        }

        [TestMethod]
        public void SetProfinetDeviceName_AnEmptyName_ThrowsInvalidParams()
        {
            var plc = ProfinetPlcNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                    plc.DevicePath, plc.InterfaceName, " ", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void SetProfinetDeviceName_NoBackupDirectory_ThrowsInvalidParams()
        {
            var plc = ProfinetPlcNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetProfinetDeviceName(
                    plc.DevicePath, plc.InterfaceName, SomeValidName, string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        private static NetworkNodeInfo ProfinetPlcNode()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();
            var plc = nodes.FirstOrDefault(node => node.DevicePath.Contains("PLC_0") && node.IsConnected);

            Assert.IsNotNull(plc, $"No connected PLC_0 interface: {Describe(nodes)}");

            return plc;
        }

        private static NetworkNodeInfo ProfibusNode()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();
            var card = nodes.FirstOrDefault(node => node.DevicePath == ProfibusCardPath);

            Assert.IsNotNull(card, $"No interface at {ProfibusCardPath}: {Describe(nodes)}");

            return card;
        }

        private static NetworkNodeInfo NodeAt(string devicePath, string interfaceName)
        {
            var node = AssemblyHooks.SharedPortal.GetNetworkTopology()
                .FirstOrDefault(one => one.DevicePath == devicePath && one.InterfaceName == interfaceName);

            Assert.IsNotNull(node, $"{devicePath} / {interfaceName} is no longer in the topology");

            return node;
        }

        private static string Describe(IEnumerable<NetworkNodeInfo> nodes)
        {
            return string.Join("; ", nodes.Select(node => $"{node.DevicePath}|{node.InterfaceName}|{node.NetworkType}|{node.ProfinetDeviceName}"));
        }
    }
}
