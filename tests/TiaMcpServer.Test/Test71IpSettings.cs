using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// A node's subnet mask and router, set through Openness.
    /// </summary>
    /// <remarks>
    /// Built as measured on 2026-11-15 on TIA Portal V21: a CPU 1511-1 PN V4.0 on a subnet, at 192.168.0.1.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test71IpSettings
    {
        private const string Cpu1511 = "OrderNumber:6ES7 511-1AL03-0AB0/V4.0";
        private const string Plc1500 = "PLC_A";
        private const string SubnetName = "PN/IE_1";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "IpSettings");
            AssemblyHooks.SharedPortal.AddDevice(Cpu1511, Plc1500);
            var node = NodeOf(Plc1500);
            AssemblyHooks.SharedPortal.CreateSubnet(node.DevicePath, node.InterfaceName, SubnetName, Backup());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void SetNodeIpSettings_MaskAndRouter_AreReadBack()
        {
            var node = NodeOf(Plc1500);

            var applied = AssemblyHooks.SharedPortal.SetNodeIpSettings(node.DevicePath, node.InterfaceName, new NodeIpSettings("255.255.255.0", "192.168.0.254"), Backup());

            Assert.AreEqual("255.255.255.0", applied.SubnetMask);
            Assert.AreEqual("192.168.0.254", applied.RouterAddress);
        }

        [TestMethod]
        public void SetNodeIpSettings_NoRouter_LeavesTheNodeWithoutOne()
        {
            var node = NodeOf(Plc1500);
            AssemblyHooks.SharedPortal.SetNodeIpSettings(node.DevicePath, node.InterfaceName, new NodeIpSettings("255.255.255.0", "192.168.0.254"), Backup());

            var applied = AssemblyHooks.SharedPortal.SetNodeIpSettings(node.DevicePath, node.InterfaceName, new NodeIpSettings("255.255.0.0", string.Empty), Backup());

            Assert.AreEqual("255.255.0.0", applied.SubnetMask);
            Assert.IsFalse(applied.UsesRouter);
        }

        [TestMethod]
        public void SetNodeIpSettings_RouterOutsideTheSubnet_ThrowsInvalidParams()
        {
            var node = NodeOf(Plc1500);

            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.SetNodeIpSettings(node.DevicePath, node.InterfaceName, new NodeIpSettings("255.255.255.0", "192.168.1.254"), Backup()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void SetNodeIpSettings_Refused_RecordsNothing()
        {
            var node = NodeOf(Plc1500);
            var backup = Backup();

            Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.SetNodeIpSettings(node.DevicePath, node.InterfaceName, new NodeIpSettings("255.255.255.0", "192.168.1.254"), backup));

            Assert.IsFalse(Directory.Exists(backup) && Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Any(), "a refused call left a backup in " + backup);
        }

        /// <remarks>The network record has addresses but no masks, so the node's own attributes are what keeps the old mask.</remarks>
        [TestMethod]
        public void SetNodeIpSettings_Applied_RecordsTheOldMaskFirst()
        {
            var node = NodeOf(Plc1500);
            var backup = Backup();

            AssemblyHooks.SharedPortal.SetNodeIpSettings(node.DevicePath, node.InterfaceName, new NodeIpSettings("255.255.0.0", string.Empty), backup);

            var recorded = string.Join(Environment.NewLine, Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
            StringAssert.Contains(recorded, "255.255.255.0", StringComparison.Ordinal);
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
