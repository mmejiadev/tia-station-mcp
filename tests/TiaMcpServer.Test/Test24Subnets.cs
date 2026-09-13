using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Subnets: reading the wires of a project, creating one, and attaching an interface to it.
    /// </summary>
    /// <remarks>
    /// Like Test13IoSystem these tests rewire the network, so each works on a copy retrieved per
    /// test rather than on the shared fixture. A test that left a station on a different subnet
    /// would fail somewhere else entirely, in a class about downloading.
    ///
    /// The load-bearing assertion is <see cref="ConnectDeviceToSubnet_NamesTakenFromTheReads_AreEnoughToAimIt"/>,
    /// and it is here because of what happened on 2026-09-05: SetDeviceAddress compiled, had its
    /// guard tested, and could not be aimed, because the paths GetNetworkTopology printed were not
    /// paths anything could resolve. The three columns this write takes come from two read tools,
    /// and nothing but a test asserting the round trip keeps them agreeing.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test24Subnets
    {
        // A PROFIBUS communications card in the fixture, unconnected. Test13IoSystem uses the same
        // one, for the same reason: it is the only interface in the project attached to nothing.
        private const string ProfibusCardPath = "PC-System_0/CP 5622_1";

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
        /// The two readers have to agree on a name, because one prints the subnet an interface is
        /// on and the other is how you find out what that subnet is. A name only one of them uses
        /// would be a dead end for anybody following the trail.
        /// </remarks>
        [TestMethod]
        public void GetSubnets_OpenProject_NamesEverySubnetTheTopologyReports()
        {
            var fromTopology = AssemblyHooks.SharedPortal.GetNetworkTopology()
                .Where(node => node.IsConnected)
                .Select(node => node.SubnetName)
                .Distinct();

            var known = AssemblyHooks.SharedPortal.GetSubnets().Select(subnet => subnet.Name).ToList();

            Assert.IsTrue(known.Count > 0, "The fixture has connected interfaces but GetSubnets found no subnet");

            foreach (var name in fromTopology)
            {
                CollectionAssert.Contains(known, name, $"The topology reports subnet '{name}' and GetSubnets does not list it");
            }
        }

        [TestMethod]
        public void CreateSubnet_AnUnconnectedInterface_PutsItOnTheNewSubnet()
        {
            var card = UnconnectedProfibusNode();

            var created = AssemblyHooks.SharedPortal.CreateSubnet(
                card.DevicePath, card.InterfaceName, "Cell_DP_Net", _backupDirectory);

            Assert.AreEqual("Cell_DP_Net", created);
            Assert.IsTrue(
                AssemblyHooks.SharedPortal.GetSubnets().Any(subnet => subnet.Name == "Cell_DP_Net"),
                "The subnet was reported as created and GetSubnets does not list it");
        }

        /// <remarks>
        /// The write is reported by reading the project back, not by echoing the argument. This is
        /// what makes the difference visible: the topology is where the next tool will look.
        /// </remarks>
        [TestMethod]
        public void CreateSubnet_AnUnconnectedInterface_ShowsUpInTheTopology()
        {
            var card = UnconnectedProfibusNode();

            AssemblyHooks.SharedPortal.CreateSubnet(card.DevicePath, card.InterfaceName, "Cell_DP_Net", _backupDirectory);

            var after = NodeAt(card.DevicePath, card.InterfaceName);

            Assert.AreEqual("Cell_DP_Net", after.SubnetName);
        }

        [TestMethod]
        public void CreateSubnet_RecordsTheNetworkBeforeChangingIt()
        {
            var card = UnconnectedProfibusNode();

            AssemblyHooks.SharedPortal.CreateSubnet(card.DevicePath, card.InterfaceName, "Cell_DP_Net", _backupDirectory);

            Assert.IsTrue(
                File.Exists(Path.Combine(_backupDirectory, "network", "topology.txt")),
                $"No network backup was written to {_backupDirectory}");
        }

        /// <remarks>
        /// Never rewire. An interface already on a subnet is refused rather than moved, because
        /// moving one is how a working network silently becomes a broken one.
        /// </remarks>
        [TestMethod]
        public void CreateSubnet_AnInterfaceAlreadyOnASubnet_IsRefused()
        {
            var plc = ConnectedPlcNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateSubnet(
                    plc.DevicePath, plc.InterfaceName, "Some_New_Name", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, plc.SubnetName, StringComparison.Ordinal);
        }

        [TestMethod]
        public void CreateSubnet_ANameAlreadyTaken_IsRefused()
        {
            var card = UnconnectedProfibusNode();
            var taken = ConnectedPlcNode().SubnetName;

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateSubnet(
                    card.DevicePath, card.InterfaceName, taken, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// Three columns, two read tools, one write. Reconnecting an interface to the subnet it is
        /// already on also happens to be the idempotence the repository requires of every write.
        /// </remarks>
        [TestMethod]
        public void ConnectDeviceToSubnet_NamesTakenFromTheReads_AreEnoughToAimIt()
        {
            var plc = ConnectedPlcNode();
            var subnet = AssemblyHooks.SharedPortal.GetSubnets().First(one => one.Name == plc.SubnetName);

            var applied = AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(
                plc.DevicePath, plc.InterfaceName, subnet.Name, _backupDirectory);

            Assert.AreEqual(subnet.Name, applied);
            Assert.AreEqual(subnet.Name, NodeAt(plc.DevicePath, plc.InterfaceName).SubnetName);
        }

        [TestMethod]
        public void ConnectDeviceToSubnet_AnUnknownSubnet_SaysWhichSubnetsThereAre()
        {
            var plc = ConnectedPlcNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(
                    plc.DevicePath, plc.InterfaceName, "NoSuchSubnet", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
            StringAssert.Contains(failure.Message, plc.SubnetName, StringComparison.Ordinal);
        }

        /// <remarks>
        /// The mismatch Openness reports as a generic failure. A PROFIBUS card cannot join an
        /// Ethernet subnet, and being told which is which is the difference between a mistake and
        /// an afternoon.
        /// </remarks>
        [TestMethod]
        public void ConnectDeviceToSubnet_ASubnetOfAnotherNetworkType_NamesBothTypes()
        {
            var card = UnconnectedProfibusNode();
            var ethernet = ConnectedPlcNode().SubnetName;

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(
                    card.DevicePath, card.InterfaceName, ethernet, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "Profibus", StringComparison.Ordinal);
        }

        /// <remarks>
        /// The other half of never rewiring: connecting an interface that is on a different subnet
        /// would unwire whatever it talks to now, so it is refused before the network types are
        /// even compared. The PLC is on its own subnet and the one created here is a second, which
        /// is all this needs.
        /// </remarks>
        [TestMethod]
        public void ConnectDeviceToSubnet_AnInterfaceOnAnotherSubnet_IsRefused()
        {
            var card = UnconnectedProfibusNode();
            var plc = ConnectedPlcNode();

            AssemblyHooks.SharedPortal.CreateSubnet(card.DevicePath, card.InterfaceName, "Cell_DP_Net", _backupDirectory);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ConnectDeviceToSubnet(
                    plc.DevicePath, plc.InterfaceName, "Cell_DP_Net", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, plc.SubnetName, StringComparison.Ordinal);
        }

        [TestMethod]
        public void GetSubnets_NoProjectOpen_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.CloseProject();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetSubnets());

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <summary>The PROFIBUS card the fixture leaves attached to nothing.</summary>
        private static NetworkNodeInfo UnconnectedProfibusNode()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();
            var card = nodes.FirstOrDefault(node => node.DevicePath == ProfibusCardPath && !node.IsConnected);

            Assert.IsNotNull(card, $"No unconnected interface at {ProfibusCardPath}: {Describe(nodes)}");

            return card;
        }

        private static NetworkNodeInfo ConnectedPlcNode()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();
            var plc = nodes.FirstOrDefault(node => node.DevicePath.Contains("PLC_0") && node.IsConnected);

            Assert.IsNotNull(plc, $"No connected PLC_0 interface: {Describe(nodes)}");

            return plc;
        }

        private static NetworkNodeInfo NodeAt(string devicePath, string interfaceName)
        {
            var node = AssemblyHooks.SharedPortal.GetNetworkTopology()
                .FirstOrDefault(one => one.DevicePath == devicePath && one.InterfaceName == interfaceName);

            Assert.IsNotNull(node, $"{devicePath} / {interfaceName} is no longer in the topology");

            return node;
        }

        private static string Describe(System.Collections.Generic.IEnumerable<NetworkNodeInfo> nodes)
        {
            return string.Join("; ", nodes.Select(node => $"{node.DevicePath}|{node.InterfaceName}|{node.NetworkType}|{node.SubnetName}"));
        }
    }
}
