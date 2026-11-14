using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Going online reaches simulated controllers only, and a network search is decided by the policy under its own
    /// prefix. These tests are those rules' assertions.
    /// </remarks>
    [TestClass]
    public sealed class PcInterfaceRuleTests
    {
        [TestMethod]
        [DataRow("PLCSIM")]
        [DataRow("Siemens PLCSIM Virtual Ethernet Adapter")]
        [DataRow("plcsim")]
        public void IsSimulation_APlcsimInterface_IsTrue(string name)
        {
            Assert.IsTrue(PcInterfaceRule.IsSimulation(name));
        }

        [TestMethod]
        [DataRow("RZ616 Wi-Fi 6E 160MHz")]
        [DataRow("Realtek PCIe GbE Family Controller")]
        [DataRow("")]
        [DataRow(null)]
        public void IsSimulation_ARealOrMissingInterface_IsFalse(string? name)
        {
            Assert.IsFalse(PcInterfaceRule.IsSimulation(name));
        }

        [TestMethod]
        public void NetworkInterface_AnInterface_IsUnderTheNetworkPrefix()
        {
            Assert.AreEqual("network/RZ616 Wi-Fi 6E 160MHz", ChangeTarget.NetworkInterface(" RZ616 Wi-Fi 6E 160MHz "));
        }

        [TestMethod]
        public void NetworkInterface_ARuleForTheProject_DoesNotAllowASearch()
        {
            var projectRule = TargetPattern.Parse(ChangeTarget.Project);

            Assert.IsFalse(projectRule.Matches(ChangeTarget.NetworkInterface("RZ616 Wi-Fi 6E 160MHz")));
        }
    }
}
