using Microsoft.Extensions.Logging;
using System;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test6Retrieve
    {
        private Portal? _portal;
        private string? _targetDirectory;

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

            // A fresh directory per test: Retrieve refuses to write over an existing project,
            // so a leftover from a previous run would fail the next one.
            _targetDirectory = Path.Combine(Path.GetTempPath(), "TiaMcpServer.Test", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Release the portal first: a retrieved project is still open and holds file handles
            // inside the directory we are about to delete.
            _portal?.Dispose();
            _portal = null;

            if (_targetDirectory != null && Directory.Exists(_targetDirectory))
            {
                Directory.Delete(_targetDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void RetrieveProject_ValidArchive_ReturnsOpenedProjectPath()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_targetDirectory);
            _portal.ConnectPortal();

            var projectPath = _portal.RetrieveProject(Settings.Project1ArchivePath, _targetDirectory);

            Assert.IsTrue(File.Exists(projectPath), $"Retrieved project file does not exist: {projectPath}");
            Assert.IsTrue(Portal.IsLocalProjectFile(projectPath), $"Retrieved path is not a project file: {projectPath}");
        }

        [TestMethod]
        public void RetrieveProject_MissingArchive_ThrowsNotFound()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_targetDirectory);
            _portal.ConnectPortal();

            var exception = Assert.ThrowsException<PortalException>(
                () => _portal.RetrieveProject(Path.Combine(_targetDirectory, "DoesNotExist.zap20"), _targetDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void RetrieveProject_NotConnected_ThrowsInvalidState()
        {
            Assert.IsNotNull(_portal);
            Assert.IsNotNull(_targetDirectory);

            var exception = Assert.ThrowsException<PortalException>(
                () => _portal.RetrieveProject(Settings.Project1ArchivePath, _targetDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
        }
    }
}
