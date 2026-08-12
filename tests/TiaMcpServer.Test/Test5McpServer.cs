using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// Covers the MCP tool layer over the same project the portal tests use. The shared portal is
    /// injected rather than letting McpServer start one of its own, since it keeps its Portal in a
    /// static field and would otherwise leave a second TIA Portal running for the rest of the run.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test5McpServer
    {
        [TestInitialize]
        public void TestInit()
        {
            McpServer.Portal = AssemblyHooks.SharedPortal;
            McpServer.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            McpServer.CloseProject();
        }

        [TestMethod]
        public void GetState_ProjectOpen_ReportsConnectedAndNamesProject()
        {
            var response = McpServer.GetState();

            Assert.IsTrue(response.IsConnected == true, "GetState reports no connection while a project is open");
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Project), "GetState does not name the open project");
        }

        [TestMethod]
        public void GetProjects_ProjectOpen_ReturnsIt()
        {
            var response = McpServer.GetProjects();

            Assert.IsNotNull(response.Items);
            Assert.IsTrue(response.Items.Any(), "No project was returned while one is open");
        }

        [TestMethod]
        public void GetProjectTree_ProjectOpen_NamesDevices()
        {
            var response = McpServer.GetProjectTree();

            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Tree), "The project tree is empty");
            Assert.IsTrue(response.Tree!.Contains("PLC_0"), $"The tree does not mention PLC_0:\n{response.Tree}");
        }

        [TestMethod]
        public void GetDevices_ProjectOpen_ReturnsDevices()
        {
            var response = McpServer.GetDevices();

            Assert.IsNotNull(response.Items);
            Assert.IsTrue(response.Items.Any(), "The project has devices but none were returned");
        }

        [TestMethod]
        public void GetSoftwareInfo_PlcSoftware_ReturnsName()
        {
            var response = McpServer.GetSoftwareInfo(Settings.Project1PlcSoftwarePath0);

            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Name), "No software name returned");
        }

        [TestMethod]
        [DataRow("HMI_0")]
        [DataRow("PC-System_0")]
        public void GetDeviceInfo_ExistingDevice_ReturnsName(string devicePath)
        {
            var response = McpServer.GetDeviceInfo(devicePath);

            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Name), $"No name returned for '{devicePath}'");
        }

        [TestMethod]
        [DataRow("PLC_0")]
        [DataRow("PC-System_0/Software PLC_0")]
        public void GetDeviceItemInfo_ExistingDeviceItem_ReturnsName(string deviceItemPath)
        {
            var response = McpServer.GetDeviceItemInfo(deviceItemPath);

            Assert.IsFalse(string.IsNullOrWhiteSpace(response.Name), $"No name returned for '{deviceItemPath}'");
        }

        [TestMethod]
        public void GetBlockInfo_ExistingBlock_ReturnsNameAndLanguage()
        {
            var response = McpServer.GetBlockInfo(Settings.Project1PlcSoftwarePath0, "1_Tests/FC_Block_1");

            Assert.AreEqual("FC_Block_1", response.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.ProgrammingLanguage), "No programming language reported");
        }

        [TestMethod]
        public void GetTypeInfo_ExistingType_ReturnsName()
        {
            var response = McpServer.GetTypeInfo(Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister/ML_SubstratState");

            Assert.AreEqual("ML_SubstratState", response.Name);
        }
    }
}
