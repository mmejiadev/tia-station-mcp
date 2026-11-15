using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The mask and router SetIpSettings writes to a node, checked before TIA Portal sees them. A router outside the
    /// node's subnet is stored by TIA Portal without complaint and leaves a PLC that never reaches its gateway.
    /// </remarks>
    [TestClass]
    public sealed class IpSettingsRuleTests
    {
        [TestMethod]
        [DataRow("255.255.255.0", 0xFFFFFF00u)]
        [DataRow("255.255.0.0", 0xFFFF0000u)]
        [DataRow("255.255.255.252", 0xFFFFFFFCu)]
        [DataRow("128.0.0.0", 0x80000000u)]
        public void RequireSubnetMask_OnesThenZeros_ReturnsTheMask(string text, uint expected)
        {
            var mask = IpSettingsRule.RequireSubnetMask(text);

            Assert.AreEqual(expected, mask);
        }

        [TestMethod]
        [DataRow("255.0.255.0")]
        [DataRow("255.255.255.1")]
        [DataRow("0.0.0.0")]
        public void RequireSubnetMask_NotARunOfOnes_ThrowsInvalidParams(string text)
        {
            var failure = Assert.ThrowsException<PortalException>(() => IpSettingsRule.RequireSubnetMask(text));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("255.255.255")]
        [DataRow("255.255.255.256")]
        [DataRow("255.255.0x0.0")]
        [DataRow("255.255.255.0.0")]
        [DataRow("")]
        [DataRow(null)]
        public void RequireSubnetMask_NotFourDecimalNumbers_ThrowsInvalidParams(string? text)
        {
            var failure = Assert.ThrowsException<PortalException>(() => IpSettingsRule.RequireSubnetMask(text));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void RequireRouter_InsideTheNodesSubnet_Passes()
        {
            IpSettingsRule.RequireRouter("192.168.10.1", "192.168.10.11", 0xFFFFFF00u);
        }

        [TestMethod]
        public void RequireRouter_OutsideTheNodesSubnet_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => IpSettingsRule.RequireRouter("192.168.11.1", "192.168.10.11", 0xFFFFFF00u));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void RequireRouter_TheNodesOwnAddress_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => IpSettingsRule.RequireRouter("192.168.10.11", "192.168.10.11", 0xFFFFFF00u));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("255.255.255.254")]
        [DataRow("255.255.255.255")]
        public void RequireSubnetMask_NoRoomForTwoHosts_ThrowsInvalidParams(string text)
        {
            var failure = Assert.ThrowsException<PortalException>(() => IpSettingsRule.RequireSubnetMask(text));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("192.168.10.0")]
        [DataRow("192.168.10.255")]
        public void RequireRouter_TheSubnetsNetworkOrBroadcastAddress_ThrowsInvalidParams(string router)
        {
            var failure = Assert.ThrowsException<PortalException>(() => IpSettingsRule.RequireRouter(router, "192.168.10.11", 0xFFFFFF00u));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void RequireRouter_InsideAWiderMask_Passes()
        {
            IpSettingsRule.RequireRouter("192.168.11.1", "192.168.10.11", 0xFFFF0000u);
        }

        [TestMethod]
        public void Format_AMask_WritesItDotted()
        {
            var text = IpSettingsRule.Format(0xFFFFFF00u);

            Assert.AreEqual("255.255.255.0", text);
        }
    }
}
