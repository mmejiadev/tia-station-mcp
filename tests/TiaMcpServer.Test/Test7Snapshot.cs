using Microsoft.Extensions.Logging;
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
        private Portal? _portal;
        private string? _workingDirectory;

        [TestInitialize]
        public void TestInit()
        {
            Openness.Initialize();

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            _portal = new Portal(loggerFactory.CreateLogger<Portal>());
            _workingDirectory = Path.Combine(Path.GetTempPath(), "TiaMcpServer.Test", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _portal?.Dispose();
            _portal = null;

            if (_workingDirectory != null && Directory.Exists(_workingDirectory))
            {
                Directory.Delete(_workingDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void ExportSourceSnapshot_RetrievedProject_WritesTextFiles()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_workingDirectory);
            _portal.ConnectPortal();
            _portal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_workingDirectory, "project"));
            var snapshotDirectory = Path.Combine(_workingDirectory, "snapshot");

            var result = _portal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, snapshotDirectory);

            Assert.IsTrue(result.Exported.Count > 0, $"Nothing was exported. Failed: {string.Join("; ", result.Failed)}");
            Assert.IsTrue(
                result.Exported.All(relative => File.Exists(Path.Combine(snapshotDirectory, relative.Replace('/', Path.DirectorySeparatorChar)))),
                "The report lists files that are not on disk");
        }

        [TestMethod]
        public void ExportSourceSnapshot_RetrievedProject_ExportsTagTables()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_workingDirectory);
            _portal.ConnectPortal();
            _portal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_workingDirectory, "project"));
            var snapshotDirectory = Path.Combine(_workingDirectory, "snapshot");

            var result = _portal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, snapshotDirectory);

            // Every PLC has at least a default tag table, and tag tables are the gap in upstream
            // that this snapshot exists to close.
            Assert.IsTrue(
                result.Exported.Any(relative => relative.StartsWith("tags/", StringComparison.Ordinal)),
                $"No tag table was exported. Exported: {string.Join("; ", result.Exported)}");
        }

        [TestMethod]
        public void ExportSourceSnapshot_UnknownSoftwarePath_ThrowsNotFound()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_workingDirectory);
            _portal.ConnectPortal();
            _portal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_workingDirectory, "project"));

            var exception = Assert.ThrowsException<PortalException>(
                () => _portal.ExportSourceSnapshot("NoSuchDevice/NoSuchPlc", Path.Combine(_workingDirectory, "snapshot")));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void ExportSourceSnapshot_NoProjectOpen_ThrowsInvalidState()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_workingDirectory);
            _portal.ConnectPortal();

            var exception = Assert.ThrowsException<PortalException>(
                () => _portal.ExportSourceSnapshot(Settings.Project1PlcSoftwarePath0, Path.Combine(_workingDirectory, "snapshot")));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }
    }
}
