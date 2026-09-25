using TiaMcpServer.Siemens;

namespace TiaMcpServer.OpcUa.Test
{
    [TestClass]
    public class OpcUaEndpointTests
    {
        [TestMethod]
        public void Parse_UrlWithPort_NamesHostAndPortInThePolicyTarget()
        {
            var endpoint = OpcUaEndpoint.Parse("opc.tcp://192.168.0.1:4840");

            Assert.AreEqual("opcua/192.168.0.1:4840", endpoint.PolicyTarget);
        }

        [TestMethod]
        public void Parse_UrlWithoutPort_UsesTheCpuDefault()
        {
            var endpoint = OpcUaEndpoint.Parse("opc.tcp://192.168.0.1");

            Assert.AreEqual(4840, endpoint.Port);
            Assert.AreEqual("opcua/192.168.0.1:4840", endpoint.PolicyTarget);
        }

        [TestMethod]
        public void Parse_UrlWithAPath_LeavesThePathOutOfThePolicyTarget()
        {
            var endpoint = OpcUaEndpoint.Parse("opc.tcp://plc-cell:4840/UA/Server");

            Assert.AreEqual("opcua/plc-cell:4840", endpoint.PolicyTarget);
            Assert.AreEqual("/UA/Server", endpoint.Url.AbsolutePath);
        }

        [TestMethod]
        public void Parse_UpperCaseHost_ProducesTheSameTargetAsLowerCase()
        {
            var upper = OpcUaEndpoint.Parse("opc.tcp://PLC-CELL:4840");
            var lower = OpcUaEndpoint.Parse("opc.tcp://plc-cell:4840");

            Assert.AreEqual(lower.PolicyTarget, upper.PolicyTarget);
        }

        [TestMethod]
        public void Parse_AnotherScheme_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(() => OpcUaEndpoint.Parse("http://192.168.0.1:4840"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void Parse_CredentialsInTheUrl_AreRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(() => OpcUaEndpoint.Parse("opc.tcp://admin:secret@192.168.0.1:4840"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void Parse_Empty_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(() => OpcUaEndpoint.Parse("  "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void Parse_AColonWithNoPort_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(() => OpcUaEndpoint.Parse("opc.tcp://localhost:"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void ToString_NoPath_PrintsNoTrailingSlash()
        {
            Assert.AreEqual("opc.tcp://192.168.0.1:4840", OpcUaEndpoint.Parse("opc.tcp://192.168.0.1").ToString());
        }

        [TestMethod]
        public void Parse_NotAUrl_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(() => OpcUaEndpoint.Parse("192.168.0.1"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }
    }
}
