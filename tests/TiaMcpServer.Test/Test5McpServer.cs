using ModelContextProtocol;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        /// <summary>An unclosed character class: not a valid regular expression.</summary>
        private const string InvalidFilter = "[";

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

        /// <remarks>
        /// An invalid name filter is the caller's mistake. The portal layer swallowed it and answered
        /// with an empty list, and the tools turned what did get through into an internal error —
        /// "no blocks" or "retry", never "your filter is wrong". One test per tool, since each had
        /// its own catch.
        /// </remarks>
        [TestMethod]
        public void GetBlocks_InvalidFilter_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<McpException>(
                () => McpServer.GetBlocks(Settings.Project1PlcSoftwarePath0, InvalidFilter));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
        }

        [TestMethod]
        public void GetTypes_InvalidFilter_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<McpException>(
                () => McpServer.GetTypes(Settings.Project1PlcSoftwarePath0, InvalidFilter));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
        }

        [TestMethod]
        public void ExportBlock_ExistingPath_ReportsSuccess()
        {
            var response = McpServer.ExportBlock(Settings.Project1PlcSoftwarePath0, "1_Tests/FC_Block_1", Path.GetTempPath());

            Assert.AreEqual(true, (bool?)response.Meta?["success"], response.Message);
        }

        /// <remarks>
        /// The suggestion used to be built inside an empty catch. The answer is still "not found",
        /// as invalid input, and it names the full path the caller most likely meant.
        /// </remarks>
        [TestMethod]
        public void ExportBlock_BareName_ThrowsInvalidParamsSuggestingTheFullPath()
        {
            var failure = Assert.ThrowsException<McpException>(
                () => McpServer.ExportBlock(Settings.Project1PlcSoftwarePath0, "FC_Block_1", Path.GetTempPath()));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
            StringAssert.Contains(failure.Message, "1_Tests/FC_Block_1");
        }

        /// <remarks>
        /// The bulk export used to answer "No blocks found" with success true for a software path
        /// that does not exist.
        /// </remarks>
        [TestMethod]
        public async Task ExportBlocks_UnknownSoftwarePath_ThrowsInvalidParams()
        {
            var failure = await Assert.ThrowsExceptionAsync<McpException>(
                () => McpServer.ExportBlocks(null!, null!, "NoSuchDevice/NoSuchPlc", Path.GetTempPath()));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
        }

        [TestMethod]
        public async Task ExportBlocks_InvalidFilter_ThrowsInvalidParams()
        {
            var failure = await Assert.ThrowsExceptionAsync<McpException>(
                () => McpServer.ExportBlocks(null!, null!, Settings.Project1PlcSoftwarePath0, Path.GetTempPath(), InvalidFilter));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
        }

        [TestMethod]
        public async Task ExportTypes_InvalidFilter_ThrowsInvalidParams()
        {
            var failure = await Assert.ThrowsExceptionAsync<McpException>(
                () => McpServer.ExportTypes(null!, null!, Settings.Project1PlcSoftwarePath0, Path.GetTempPath(), InvalidFilter));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
        }

        [TestMethod]
        public async Task ExportBlocksAsDocuments_InvalidFilter_ThrowsInvalidParams()
        {
            var failure = await Assert.ThrowsExceptionAsync<McpException>(
                () => McpServer.ExportBlocksAsDocuments(null!, null!, Settings.Project1PlcSoftwarePath0, Path.GetTempPath(), InvalidFilter));

            Assert.AreEqual(McpErrorCode.InvalidParams, failure.ErrorCode, failure.Message);
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
