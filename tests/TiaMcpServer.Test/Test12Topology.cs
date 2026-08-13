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
