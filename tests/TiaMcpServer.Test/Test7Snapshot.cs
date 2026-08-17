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
        public void ExportSourceSnapshot_RetrievedProject_RecordsTheNetwork()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
            var snapshotDirectory = Path.Combine(_testDirectory, "snapshot");

            var result = AssemblyHooks.SharedPortal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, snapshotDirectory);

            // The same blocks addressing a device at a different address are a different system.
            // A snapshot that cannot show that is not describing the project.
            CollectionAssert.Contains(result.Exported.ToList(), "network/topology.txt");
            var topology = File.ReadAllText(Path.Combine(snapshotDirectory, "network", "topology.txt"));
            StringAssert.Contains(topology, "192.168.0.1", "The topology does not record the PLC address");
        }

        [TestMethod]
        public void ExportSourceSnapshot_RunTwice_ProducesAnIdenticalTopologyFile()
        {
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            var first = ExportTopologyTo(Path.Combine(_testDirectory, "snapshot1"));
            var second = ExportTopologyTo(Path.Combine(_testDirectory, "snapshot2"));

            // Line order that follows whatever order TIA happened to enumerate devices in would
            // produce phantom diffs, and phantom diffs train everyone to ignore real ones.
            Assert.AreEqual(first, second, "Two snapshots of an unchanged project differ");
        }

        private static string ExportTopologyTo(string snapshotDirectory)
        {
            AssemblyHooks.SharedPortal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, snapshotDirectory);

            return File.ReadAllText(Path.Combine(snapshotDirectory, "network", "topology.txt"));
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
