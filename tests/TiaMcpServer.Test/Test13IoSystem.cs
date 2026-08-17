using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// These tests modify the project, so they work on a copy retrieved per test rather than on
    /// the shared one. A test that rewires the network of a fixture every other test depends on
    /// would be a slow, confusing failure somewhere else.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test13IoSystem
    {
        // A PROFIBUS communications card in the fixture, unconnected and at station address 2.
        private const string ProfibusCardPath = "PC-System_0/CP 5622_1";

        private string _testDirectory = string.Empty;
        private string _backupDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            _backupDirectory = Path.Combine(_testDirectory, "backup");

            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CreateIoSystem_OnThePlc_ReturnsTheSubnetItLandedOn()
        {
            var subnet = AssemblyHooks.SharedPortal.CreateIoSystem(Settings.Project1PlcSoftwarePath0, "Cell_IO", _backupDirectory);

            Assert.IsFalse(string.IsNullOrWhiteSpace(subnet), "No subnet was reported for the new IO system");
        }

        [TestMethod]
        public void CreateIoSystem_RecordsTheNetworkBeforeChangingIt()
        {
            AssemblyHooks.SharedPortal.CreateIoSystem(Settings.Project1PlcSoftwarePath0, "Cell_IO", _backupDirectory);

            // Rewiring a network without recording what it was is exactly the kind of change that
            // cannot be undone by reading the result.
            Assert.IsTrue(
                File.Exists(Path.Combine(_backupDirectory, "network", "topology.txt")),
                $"No network backup was written to {_backupDirectory}");
        }

        [TestMethod]
        public void CreateIoSystem_OnAProfibusCard_UsesTheSameModelAsProfinet()
        {
            // The fixture carries CP 5622 PROFIBUS cards, unconnected, at station address 2.
            // Openness has no separate DP master API — no DpMasterSystem type exists — so a
            // PROFIBUS master system is created through exactly the same IoController call.
            // This test is what turns that reading of the API into a checked fact.
            var subnet = AssemblyHooks.SharedPortal.CreateIoSystem(ProfibusCardPath, "Cell_DP", _backupDirectory);

            Assert.IsFalse(string.IsNullOrWhiteSpace(subnet), "No subnet was reported for the DP master system");

            var profibusNode = AssemblyHooks.SharedPortal.GetNetworkTopology()
                .FirstOrDefault(node => node.DevicePath.Contains("CP 5622") && node.NetworkType == "Profibus");

            Assert.IsNotNull(profibusNode, "The PROFIBUS card disappeared from the topology");
            Assert.IsTrue(profibusNode.IsConnected, "The PROFIBUS card is still on no subnet after creating a master system");
        }

        [TestMethod]
        public void CreateIoSystem_NoBackupDirectory_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateIoSystem(Settings.Project1PlcSoftwarePath0, "Cell_IO", string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void CreateIoSystem_UnknownDevice_ThrowsNotFound()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CreateIoSystem("NoSuchDevice", "Cell_IO", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void AssignDeviceToIoSystem_TheControllerItself_IsRefused()
        {
            // A CPU is the IO controller, not an IO device: it has no IoConnector and cannot join
            // its own IO system. The check for that comes before the check for the IO system name,
            // deliberately — "this device cannot join an IO system" is more useful than "no such
            // IO system" when both are true.
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.AssignDeviceToIoSystem(Settings.Project1PlcSoftwarePath0, "NoSuchIoSystem", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
            StringAssert.Contains(exception.Message, "join an IO system");
        }

        [TestMethod]
        public void AssignDeviceToIoSystem_UnknownDevice_ThrowsNotFound()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.AssignDeviceToIoSystem("NoSuchDevice", "Cell_IO", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }
    }
}
