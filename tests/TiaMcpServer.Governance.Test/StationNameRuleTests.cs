using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A station named <c>Cell/PLC_1</c> would be the policy target of <c>PLC_1</c> in group <c>Cell</c>, so a rule meant for
    /// the group would let a session create a station outside it.
    /// </remarks>
    [TestClass]
    public sealed class StationNameRuleTests
    {
        [TestMethod]
        public void RequireStationName_ANameWithTheSeparator_IsInvalidParams()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => StationNameRule.RequireStationName("deviceName", "Cell/PLC_1"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void RequireStationName_APlainName_IsAccepted()
        {
            StationNameRule.RequireStationName("deviceName", "PLC_1");
        }

        [TestMethod]
        public void RequireGroupName_APath_IsInvalidParams()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => StationNameRule.RequireGroupName("Cell/Line"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void RequireGroupName_OneGroup_IsAccepted()
        {
            StationNameRule.RequireGroupName("Cell");
        }
    }
}
