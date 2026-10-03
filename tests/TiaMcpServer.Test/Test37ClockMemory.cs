using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The system and clock memory: setting it where Openness reaches it, and saying what to do where
    /// it does not.
    /// </summary>
    /// <remarks>
    /// Class exercises lean on <c>Clock_1Hz</c> and <c>FirstScan</c>, and on an S7-1200 TIA Portal
    /// V20 does not expose the setting to Openness (measured on 2026-10-03). Siemens documents it for
    /// the S7-1500, which is what <c>PLC_0</c> in the test project is, so these tests measure that
    /// claim rather than take it from the PDF.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test37ClockMemory
    {
        private const string ClockMemory = "ClockMemoryByte";

        // An HMI runtime: a device item that has no system or clock memory of any kind.
        private const string DeviceItemWithoutClockMemory = "HMI_0/HMI_RT_1";

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
        public void SetDeviceParameter_ClockMemoryOnAnS71500_HoldsTrueAfterwards()
        {
            var applied = AssemblyHooks.SharedPortal.SetDeviceParameter(
                Settings.Project1PlcSoftwarePath0, ClockMemory, "true", _backupDirectory);

            Assert.AreEqual(true, applied.Value);
        }

        /// <remarks>
        /// The tags are what a program uses, so enabling the setting is only half of it. Every table
        /// is searched rather than the default one by name, because that name is in the language the
        /// project was created in.
        /// </remarks>
        [TestMethod]
        public void SetDeviceParameter_ClockMemoryOnAnS71500_CreatesTheClockTags()
        {
            AssemblyHooks.SharedPortal.SetDeviceParameter(
                Settings.Project1PlcSoftwarePath0, ClockMemory, "true", _backupDirectory);

            var tagNames = AssemblyHooks.SharedPortal.GetTagTables(Settings.Project1PlcSoftwarePath0)
                .SelectMany(table => AssemblyHooks.SharedPortal.GetTags(Settings.Project1PlcSoftwarePath0, table.Path))
                .Select(tag => tag.Name)
                .ToList();

            CollectionAssert.Contains(tagNames, "Clock_1Hz", $"Tags after enabling: {string.Join(", ", tagNames)}");
        }

        [TestMethod]
        public void SetDeviceParameter_ClockMemoryOnADeviceWithoutIt_SaysToTickItByHand()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetDeviceParameter(
                    DeviceItemWithoutClockMemory, ClockMemory, "true", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
            StringAssert.Contains(failure.Message, "General > System and clock memory", StringComparison.Ordinal);
        }
    }
}
