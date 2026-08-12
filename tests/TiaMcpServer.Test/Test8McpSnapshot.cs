using ModelContextProtocol;
using System;
using System.IO;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// Exercises the MCP tool layer rather than the portal directly, so the error mapping and the
    /// response messages are covered too. McpServer keeps its Portal in a static field, so the
    /// shared portal is injected instead of letting it start one of its own.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test8McpSnapshot
    {
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            McpServer.Portal = AssemblyHooks.SharedPortal;
            _testDirectory = AssemblyHooks.CreateTestDirectory();
        }

        [TestMethod]
        public void RetrieveProject_ValidArchive_ReturnsProjectPath()
        {
            var response = McpServer.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            Assert.IsTrue(File.Exists(response.ProjectPath), $"Project file does not exist: {response.ProjectPath}");
        }

        [TestMethod]
        public void ExportSourceSnapshot_RetrievedProject_ReportsExportedAndUnsupported()
        {
            McpServer.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            var response = McpServer.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, Path.Combine(_testDirectory, "snapshot"));

            Assert.IsTrue(response.Exported.Count > 0, "Nothing was exported");
            Assert.IsTrue(response.Unsupported.Count > 0, "Expected LAD blocks to be reported as unsupported");
            // A caller who never inspects Unsupported must still not mistake a partial snapshot
            // for a complete one, so the message has to say it out loud.
            Assert.IsTrue(
                response.Message != null && response.Message.Contains("no text representation", StringComparison.Ordinal),
                $"The message hides that the snapshot is partial: '{response.Message}'");
        }

        [TestMethod]
        public void ExportSourceSnapshot_UnknownSoftwarePath_ThrowsInvalidParams()
        {
            McpServer.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            var exception = Assert.ThrowsException<McpException>(
                () => McpServer.ExportSourceSnapshot("NoSuchDevice/NoSuchPlc", Path.Combine(_testDirectory, "snapshot")));

            // A missing path is the caller's mistake, so it must not surface as InternalError.
            Assert.AreEqual(McpErrorCode.InvalidParams, exception.ErrorCode);
        }

        [TestMethod]
        public void RetrieveProject_ExistingTarget_ThrowsInvalidParams()
        {
            var target = Path.Combine(_testDirectory, "project");
            McpServer.RetrieveProject(Settings.Project1ArchivePath, target);

            var exception = Assert.ThrowsException<McpException>(
                () => McpServer.RetrieveProject(Settings.Project1ArchivePath, target));

            Assert.AreEqual(McpErrorCode.InvalidParams, exception.ErrorCode);
        }
    }
}
