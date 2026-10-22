using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Plugging an unmeasured module into a PC station once took TIA Portal down, so only the measured
    /// HMI runtimes pass. These tests are the rule's assertion.
    /// </remarks>
    [TestClass]
    public sealed class PcHmiRuntimeTests
    {
        [TestMethod]
        [DataRow("OrderNumber:6AV2 155-xxxxx-xxxx/20.0.0.2")]
        [DataRow("OrderNumber:6AV2 104-0xxxx-xxxx/17.0.0.0")]
        [DataRow("ordernumber:6av2 155-xxxxx-xxxx/20.0.0.2")]
        public void RequireMeasured_MeasuredRuntime_DoesNotThrow(string runtime)
        {
            PcHmiRuntime.RequireMeasured(runtime);
        }

        [TestMethod]
        [DataRow("OrderNumber:6ES7 672-7AC01-0YA0/V30.1")]
        [DataRow("OrderNumber:6AV2 124-0JC01-0AX0/17.0.0.0")]
        [DataRow("6AV2 155-xxxxx-xxxx")]
        public void RequireMeasured_AnythingElse_ThrowsInvalidParams(string runtime)
        {
            var failure = Assert.ThrowsException<PortalException>(() => PcHmiRuntime.RequireMeasured(runtime));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("OrderNumber:6AV2 155-xxxxx-xxxx/19.0.0.0")]
        [DataRow("OrderNumber:6AV2 104-0xxxx-xxxx/18.0.0.0")]
        [DataRow("OrderNumber:6AV2 155-bogus/20.0.0.2")]
        public void RequireMeasured_UnmeasuredVersionOfMeasuredRuntime_ThrowsInvalidParams(string runtime)
        {
            var failure = Assert.ThrowsException<PortalException>(() => PcHmiRuntime.RequireMeasured(runtime));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("System:Rack.PC")]
        [DataRow("system:rack.pc")]
        public void RequireNotPcStationRack_PcStationRack_ThrowsInvalidParams(string container)
        {
            var failure = Assert.ThrowsException<PortalException>(() => PcHmiRuntime.RequireNotPcStationRack(container));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow("OrderNumber:6ES7 215-1AG40-0XB0/V4.7")]
        [DataRow(null)]
        public void RequireNotPcStationRack_AnyOtherContainer_DoesNotThrow(string? container)
        {
            PcHmiRuntime.RequireNotPcStationRack(container);
        }

        [TestMethod]
        public void RequireMeasured_Blank_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => PcHmiRuntime.RequireMeasured(" "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
