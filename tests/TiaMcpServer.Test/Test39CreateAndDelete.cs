using System.IO;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Creating a project, adding a device to it, and deleting a block.
    /// </summary>
    /// <remarks>
    /// What is asserted is mostly what these refuse. Each of them can destroy work that is not in
    /// any backup — an open project closed unsaved, a station silently renamed, the wrong block
    /// matched by a pattern — and every one of those refusals is a decision taken here, not TIA's.
    ///
    /// Every test works on a copy of the project retrieved per test, or on a project it creates.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test39CreateAndDelete
    {
        private const string CpuTypeIdentifier = "OrderNumber:6ES7 511-1AL03-0AB0/V4.0";
        private const string BlockName = "FC_ToDelete";

        private const string BlockToDelete = @"FUNCTION ""FC_ToDelete"" : Void
VERSION : 0.1
BEGIN
    ; // deliberately empty body
END_FUNCTION
";

        private string _testDirectory = string.Empty;
        private string _backupDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            _backupDirectory = Path.Combine(_testDirectory, "backup");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void DeleteBlock_ExistingBlock_RemovesIt()
        {
            RetrieveProjectWithBlockToDelete();

            AssemblyHooks.SharedPortal.DeleteBlock(Settings.Project1PlcSoftwarePath0, BlockName, _backupDirectory);

            Assert.IsNull(AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, BlockName));
        }

        [TestMethod]
        public void DeleteBlock_ExistingBlock_ExportsItToTheBackupFirst()
        {
            RetrieveProjectWithBlockToDelete();

            AssemblyHooks.SharedPortal.DeleteBlock(Settings.Project1PlcSoftwarePath0, BlockName, _backupDirectory);

            var saved = Directory.EnumerateFiles(_backupDirectory, BlockName + ".*", SearchOption.AllDirectories);
            Assert.IsTrue(saved.Any(), $"'{BlockName}' was deleted and no copy of it is in {_backupDirectory}");
        }

        /// <remarks>
        /// The rule that matters most here: a pattern that reads would match the first block it
        /// meets, and a delete must never remove a block nobody named.
        /// </remarks>
        [TestMethod]
        public void DeleteBlock_NamePattern_ThrowsInvalidParamsAndDeletesNothing()
        {
            RetrieveProjectWithBlockToDelete();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.DeleteBlock(Settings.Project1PlcSoftwarePath0, "FC_To.*", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.IsNotNull(AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, BlockName));
        }

        [TestMethod]
        public void DeleteBlock_MissingBlock_ThrowsNotFound()
        {
            RetrieveProject();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.DeleteBlock(Settings.Project1PlcSoftwarePath0, "FC_NoSuchBlock", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// Through the MCP tool, because the target it hands the guard is built there: the block's
        /// full path, which the suite's policy allows under <c>PLC_0/*</c>.
        /// </remarks>
        [TestMethod]
        public void DeleteBlockTool_AllowedTarget_LeavesItsBackupWhereListBackupsFindsIt()
        {
            RetrieveProjectWithBlockToDelete();

            var response = McpServer.DeleteBlock(Settings.Project1PlcSoftwarePath0, BlockName);

            var backup = McpServer.ListBackups().Items
                .FirstOrDefault(item => item.Tool == "DeleteBlock" && item.Target == $"{Settings.Project1PlcSoftwarePath0}/{BlockName}");
            Assert.IsNotNull(backup, $"the delete left no backup the registry knows about: {response.Message}");
        }

        [TestMethod]
        public void AddDevice_NameAlreadyUsed_ThrowsInvalidState()
        {
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "AddDeviceTwice");
            AssemblyHooks.SharedPortal.AddDevice(CpuTypeIdentifier, "PLC_1");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.AddDevice(CpuTypeIdentifier, "PLC_1"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>
        /// A new device gives its item the same name, so a name another device's item has is taken too:
        /// TestProject1's panel is the device HMI_0 holding the item HMI_RT_1.
        /// </remarks>
        [TestMethod]
        public void AddDevice_NameOfAnItemInAnotherDevice_ThrowsInvalidState()
        {
            RetrieveProject();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.AddDevice(CpuTypeIdentifier, "HMI_RT_1"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>A name holding a path would make the existing-folder check look somewhere TIA Portal does not create.</remarks>
        [TestMethod]
        public void CreateProject_NameHoldingAPath_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "Sub/Plant"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void CreateProject_ProjectFolderAlreadyExists_ThrowsInvalidState()
        {
            Directory.CreateDirectory(Path.Combine(_testDirectory, "Existing"));

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "Existing"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <remarks>
        /// Portal.CreateProject closes the open project without saving, which the test bench wants.
        /// The tool must not: whoever calls it over MCP may have unsaved work open.
        /// </remarks>
        [TestMethod]
        public void CreateProjectTool_WhileAProjectIsOpen_IsRejectedAsBadInput()
        {
            RetrieveProject();

            var exception = Assert.ThrowsException<global::ModelContextProtocol.McpException>(
                () => McpServer.CreateProject(_testDirectory, "WouldCloseTheOpenOne"));

            Assert.AreEqual(global::ModelContextProtocol.McpErrorCode.InvalidParams, exception.ErrorCode);
        }

        [TestMethod]
        public void CreateProjectTool_NoProjectOpen_CreatesAndOpensIt()
        {
            AssemblyHooks.SharedPortal.CloseProject();

            McpServer.CreateProject(_testDirectory, "CreatedThroughTheTool");

            StringAssert.Contains(AssemblyHooks.SharedPortal.OpenProjectPath, "CreatedThroughTheTool");
        }

        private void RetrieveProject()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
        }

        private void RetrieveProjectWithBlockToDelete()
        {
            RetrieveProject();
            AssemblyHooks.SharedPortal.WriteScl(Settings.Project1PlcSoftwarePath0, BlockToDelete, Path.Combine(_testDirectory, "write-backup"));
        }
    }
}
