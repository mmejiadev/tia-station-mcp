using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// A PLC left without passwords, the way the class uses it.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-11 on TIA Portal V21: a new CPU 1512C-1 PN V2.9 asks for a password for its confidential
    /// configuration data and for full access, and its hardware does not compile until both are dealt with. The
    /// class's PLC_2 of prova-plcsimadvanced was in that state and did not download.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test65PlcAccess
    {
        private const string Cpu1512 = "OrderNumber:6ES7 512-1CK01-0AB0/V2.9";
        private const string PlcName = "PLC_1";

        /// <summary>The station AddDevice makes is named like its CPU, so the CPU is the station's item of the same name.</summary>
        private const string PlcItemPath = PlcName + "/" + PlcName;

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.CreateProject(_testDirectory, "PlcAccess");
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, PlcName);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        /// <remarks>Why the tool exists: without it the new PLC's hardware has errors.</remarks>
        [TestMethod]
        public void CompileHardware_NewS71500AsTiaPortalCreatesIt_HasErrors()
        {
            var report = AssemblyHooks.SharedPortal.CompileHardware(PlcItemPath);

            Assert.IsTrue(report.ErrorCount > 0, "a new S7-1500 compiled without passwords; the tool may no longer be needed");
        }

        [TestMethod]
        public void SetPlcFullAccess_NewS71500_HardwareCompilesWithoutErrors()
        {
            AssemblyHooks.SharedPortal.SetPlcFullAccess(PlcItemPath, Path.Combine(_testDirectory, "backup"));

            var report = AssemblyHooks.SharedPortal.CompileHardware(PlcItemPath);

            Assert.AreEqual(0, report.ErrorCount, string.Join(" | ", report.Errors.Select(message => message.Description)));
        }

        [TestMethod]
        public void SetPlcFullAccess_NewS71500_ReadsBackFullAccessAndNoProtection()
        {
            var change = AssemblyHooks.SharedPortal.SetPlcFullAccess(PlcItemPath, Path.Combine(_testDirectory, "backup")).Plcs.Single();

            Assert.AreEqual("FullAccess", change.After.AccessLevel);
            Assert.AreEqual("None", change.After.ConfidentialData);
        }

        [TestMethod]
        public void SetPlcFullAccess_NewS71500_RecordsTheProtectionItHadFirst()
        {
            var backup = Path.Combine(_testDirectory, "backup");

            var change = AssemblyHooks.SharedPortal.SetPlcFullAccess(PlcItemPath, backup).Plcs.Single();

            var record = Directory.GetFiles(backup, "*.txt", SearchOption.AllDirectories).Select(File.ReadAllText).Single();
            StringAssert.Contains(record, "ConfidentialDataProtection | " + change.Before.ConfidentialData);
            StringAssert.Contains(record, "AccessLevel | " + change.Before.AccessLevel);
        }

        [TestMethod]
        public void SetPlcFullAccess_NoBackupDirectory_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => AssemblyHooks.SharedPortal.SetPlcFullAccess(PlcItemPath, " "));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void SetPlcFullAccess_ItemWithNoPlc_ThrowsInvalidParams()
        {
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPcV21);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetPlcFullAccess("PC-System_1/HMI_RT_1", Path.Combine(_testDirectory, "backup")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// Measured 2026-11-12: an S7-1200 V4.7 has no access-level service, its access going through users and roles, so
        /// only its confidential data is opened. It still needs a user with full access to compile, which this does not make.
        /// </remarks>
        [TestMethod]
        public void SetPlcFullAccess_NewS71200_OpensItsConfidentialDataAndHasNoAccessLevel()
        {
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 215-1AG40-0XB0/V4.7", "PLC_1200");

            var change = AssemblyHooks.SharedPortal.SetPlcFullAccess("PLC_1200/PLC_1200", Path.Combine(_testDirectory, "backup")).Plcs.Single();

            Assert.AreEqual("None", change.After.ConfidentialData);
            Assert.AreEqual("UsersAndRoles", change.After.AccessLevel);
        }

        /// <remarks>
        /// Measured 2026-11-13: with its confidential data open an S7-1200 still wants "a user with Full access"; the
        /// project's Anonymous user given PLC administrator settles it, with one warning that it is for testing.
        /// </remarks>
        [TestMethod]
        public void SetPlcFullAccess_NewS71200_CompilesWithTheAnonymousUserAsAdministrator()
        {
            AssemblyHooks.SharedPortal.AddDevice("OrderNumber:6ES7 215-1AG40-0XB0/V4.7", "PLC_1200");

            var report = AssemblyHooks.SharedPortal.SetPlcFullAccess("PLC_1200/PLC_1200", Path.Combine(_testDirectory, "backup"));

            StringAssert.Contains(report.AnonymousUser, "Anonymous", System.StringComparison.Ordinal);
            var hardware = AssemblyHooks.SharedPortal.CompileHardware("PLC_1200/PLC_1200");
            Assert.AreEqual(0, hardware.ErrorCount, string.Join(" | ", hardware.Errors.Select(message => message.Description)));
        }

        /// <remarks>An S7-1500 has an access level of its own, so the project's anonymous user is left alone.</remarks>
        [TestMethod]
        public void SetPlcFullAccess_NewS71500_LeavesTheAnonymousUserAlone()
        {
            var report = AssemblyHooks.SharedPortal.SetPlcFullAccess(PlcItemPath, Path.Combine(_testDirectory, "backup"));

            Assert.AreEqual(string.Empty, report.AnonymousUser);
        }

        /// <remarks>What AddDevice does through the MCP tool for every PLC it adds.</remarks>
        [TestMethod]
        public void SetFullAccessOfAddedDevice_NewS71500_LeavesItAtFullAccess()
        {
            AssemblyHooks.SharedPortal.AddDevice(Cpu1512, "PLC_2");

            var change = AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice("PLC_2", Path.Combine(_testDirectory, "backup")).Plcs.Single();

            Assert.AreEqual("FullAccess", change.After.AccessLevel);
        }

        [TestMethod]
        public void SetFullAccessOfAddedDevice_StationWithNoPlc_ChangesNothing()
        {
            AssemblyHooks.SharedPortal.AddPcStation("PC-System_1", PcHmiRuntime.UnifiedPcV21);

            var report = AssemblyHooks.SharedPortal.SetFullAccessOfAddedDevice("PC-System_1", Path.Combine(_testDirectory, "backup"));

            Assert.AreEqual(0, report.Plcs.Count);
            Assert.AreEqual(string.Empty, report.AnonymousUser);
        }
    }
}
