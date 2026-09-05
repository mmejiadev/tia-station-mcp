using System;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test12Topology
    {
        [TestInitialize]
        public void TestInit()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void GetNetworkTopology_OpenProject_FindsThePlcInterface()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();

            Assert.IsTrue(nodes.Count > 0, "No network interface was found in a project that has devices");
            Assert.IsTrue(
                nodes.Any(node => node.DevicePath.Contains("PLC_0")),
                $"PLC_0 has no interface in the topology: {string.Join("; ", nodes.Select(n => n.DevicePath))}");
        }

        [TestMethod]
        public void GetNetworkTopology_PlcInterface_ReportsItsAddressAndSubnet()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();

            // The address the download has to reach. It was read by hand while debugging the
            // download; the point of this tool is that nobody should have to do that again.
            var plcNode = nodes.FirstOrDefault(node => node.Address == "192.168.0.1");

            Assert.IsNotNull(plcNode, $"No interface at 192.168.0.1: {string.Join("; ", nodes.Select(n => $"{n.DevicePath}={n.Address}"))}");
            Assert.IsTrue(plcNode.IsConnected, "The PLC interface reports no subnet");
        }

        /// <remarks>
        /// The address is put back in a finally, and that is not tidiness. Test11Download reaches
        /// 192.168.0.1 in the same shared project, so a test that left the CPU somewhere else would
        /// not fail here -- it would fail there, in a suite about downloading, pointing at the wrong
        /// code entirely.
        /// </remarks>
        [TestMethod]
        public void SetNodeAddress_AnInterface_MovesItAndReportsWhereItLanded()
        {
            var before = PlcNode();
            var applied = string.Empty;

            try
            {
                applied = AssemblyHooks.SharedPortal.SetNodeAddress(
                    before.DevicePath, before.InterfaceName, "192.168.0.42", AssemblyHooks.CreateTestDirectory());

                Assert.AreEqual("192.168.0.42", applied);
                Assert.AreEqual("192.168.0.42", PlcNode().Address, "the topology still reports the old address");
            }
            finally
            {
                AssemblyHooks.SharedPortal.SetNodeAddress(
                    before.DevicePath, before.InterfaceName, before.Address, AssemblyHooks.CreateTestDirectory());
            }
        }

        /// <remarks>
        /// The two names this write takes are two columns of GetNetworkTopology. Asserting that they
        /// round-trip is what keeps the read tool usable as the write tool's manual.
        /// </remarks>
        [TestMethod]
        public void SetNodeAddress_NamesTakenFromTheTopology_AreEnoughToAimIt()
        {
            var node = PlcNode();

            var applied = AssemblyHooks.SharedPortal.SetNodeAddress(
                node.DevicePath, node.InterfaceName, node.Address, AssemblyHooks.CreateTestDirectory());

            Assert.AreEqual(node.Address, applied);
        }

        /// <remarks>
        /// Naming the nodes that do exist, rather than saying "not found". The alternative sends
        /// somebody back into TIA Portal to look up a name this server was already holding.
        /// </remarks>
        [TestMethod]
        public void SetNodeAddress_UnknownNode_SaysWhichNodesThereAre()
        {
            var node = PlcNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetNodeAddress(
                    node.DevicePath, "NoSuchNode", "192.168.0.42", AssemblyHooks.CreateTestDirectory()));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
            StringAssert.Contains(failure.Message, node.InterfaceName, StringComparison.Ordinal);
        }

        [TestMethod]
        public void SetNodeAddress_NoBackupDirectory_IsRefusedBecauseThisRewiresTheProject()
        {
            var node = PlcNode();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetNodeAddress(node.DevicePath, node.InterfaceName, "192.168.0.42", string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <summary>The PLC interface, found the way a caller would find it.</summary>
        private static NetworkNodeInfo PlcNode()
        {
            var nodes = AssemblyHooks.SharedPortal.GetNetworkTopology();
            var plcNode = nodes.FirstOrDefault(node => node.DevicePath.Contains("PLC_0") && node.Address.Length > 0);

            Assert.IsNotNull(plcNode, $"No addressable PLC_0 interface: {string.Join("; ", nodes.Select(n => $"{n.DevicePath}={n.Address}"))}");

            return plcNode;
        }

        [TestMethod]
        public void GetNetworkTopology_NoProjectOpen_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.CloseProject();

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetNetworkTopology());

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }
    }
}
