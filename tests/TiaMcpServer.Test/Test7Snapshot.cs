using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test7Snapshot
    {
        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
        }

        [TestMethod]
        public void ExportSourceSnapshot_RetrievedProject_WritesTextFiles()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
            var snapshotDirectory = Path.Combine(_testDirectory, "snapshot");

            var result = AssemblyHooks.SharedPortal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, snapshotDirectory);

            Assert.IsTrue(result.Exported.Count > 0, $"Nothing was exported. Failed: {string.Join("; ", result.Failed)}");
            Assert.IsTrue(
                result.Exported.All(relative => File.Exists(Path.Combine(snapshotDirectory, relative.Replace('/', Path.DirectorySeparatorChar)))),
                "The report lists files that are not on disk");
        }

        [TestMethod]
        public void ExportSourceSnapshot_RetrievedProject_ExportsTagTables()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
            var snapshotDirectory = Path.Combine(_testDirectory, "snapshot");

            var result = AssemblyHooks.SharedPortal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, snapshotDirectory);

            // Every PLC has at least a default tag table, and tag tables are the gap in upstream
            // that this snapshot exists to close.
            Assert.IsTrue(
                result.Exported.Any(relative => relative.StartsWith("tags/", StringComparison.Ordinal)),
                $"No tag table was exported. Exported: {string.Join("; ", result.Exported)}");
        }

        [TestMethod]
        public void ExportSourceSnapshot_RetrievedProject_ReportsGraphicalBlocksAsUnsupported()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            var result = AssemblyHooks.SharedPortal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, Path.Combine(_testDirectory, "snapshot"));

            // TestProject1 contains LAD blocks. They cannot be represented as text, and a snapshot
            // that quietly omitted them would look complete while missing the main program.
            Assert.IsTrue(result.Unsupported.Count > 0, "Expected LAD blocks to be reported");
            Assert.IsFalse(result.IsComplete is false && result.Failed.Count > 0, $"Unexpected failures: {string.Join("; ", result.Failed)}");
        }

        [TestMethod]
        public void ExportSourceSnapshot_UnknownSoftwarePath_ThrowsNotFound()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportSourceSnapshot("NoSuchDevice/NoSuchPlc", Path.Combine(_testDirectory, "snapshot")));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void ExportSourceSnapshot_EmptySoftwarePath_ThrowsInvalidParams()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.ExportSourceSnapshot("  ", Path.Combine(_testDirectory, "snapshot")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }
    }
}
