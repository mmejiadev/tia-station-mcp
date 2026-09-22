using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// What a device is set to: reading the parameters that can be changed, and changing one.
    /// </summary>
    /// <remarks>
    /// Nothing here names a parameter as a constant, and that is deliberate. Which attributes a CPU
    /// exposes and which of them are writable depends on the device and on the TIA version, so a
    /// test written around "ProtectionLevel" would be testing this machine's catalogue rather than
    /// the tool. Each test finds a parameter of the shape it needs and says what it found when it
    /// cannot — which is also how anybody reading a failure learns what this CPU actually offers.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test30Parameters
    {
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
        public void GetDeviceParameters_ThePlc_ListsOnlyWhatCanBeChanged()
        {
            var parameters = AssemblyHooks.SharedPortal.GetDeviceParameters(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(parameters.Count > 0, "The CPU reports no writable parameter at all");
            Assert.IsTrue(
                parameters.All(parameter => parameter.AccessMode == "ReadWrite" || parameter.AccessMode == "Write"),
                $"A read-only parameter is in the writable list: {Describe(parameters)}");
        }

        /// <remarks>
        /// The whole bag is much bigger than the writable part, and that gap is the reason this
        /// read exists beside GetDeviceItemInfo: a caller aiming a write needs the short list.
        /// </remarks>
        [TestMethod]
        public void GetDeviceParameters_ThePlc_IsASubsetOfWhatGetDeviceItemInfoPrints()
        {
            var writable = AssemblyHooks.SharedPortal.GetDeviceParameters(Settings.Project1PlcSoftwarePath0);
            var everything = AssemblyHooks.SharedPortal.GetDeviceItem(Settings.Project1PlcSoftwarePath0);

            Assert.IsNotNull(everything);
            Assert.IsTrue(
                writable.Count < everything.Attributes.Count,
                $"Every attribute of the CPU is writable, which cannot be right: {writable.Count} of {everything.Attributes.Count}");
        }

        [TestMethod]
        public void GetDeviceParameters_AnUnknownPath_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetDeviceParameters("NoSuchDevice"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// Read back rather than echoed, like every other write here: what the caller asked for
        /// says nothing about what TIA stored.
        /// </remarks>
        [TestMethod]
        public void SetDeviceParameter_AYesOrNoParameter_HoldsTheOtherValueAfterwards()
        {
            var parameter = AWritableBoolean();
            var wanted = !(bool)parameter.Value!;

            var applied = AssemblyHooks.SharedPortal.SetDeviceParameter(
                Settings.Project1PlcSoftwarePath0,
                parameter.Name,
                wanted.ToString(),
                _backupDirectory);

            Assert.AreEqual(wanted, applied.Value);
            Assert.AreEqual(wanted, ParameterCalled(parameter.Name).Value);
        }

        /// <remarks>
        /// The name is matched without regard to case, so the write has to hand Openness the
        /// attribute's own spelling rather than the caller's.
        /// </remarks>
        [TestMethod]
        public void SetDeviceParameter_ANameInOtherCase_SetsTheParameterItNames()
        {
            var parameter = AWritableBoolean();
            var wanted = !(bool)parameter.Value!;

            var applied = AssemblyHooks.SharedPortal.SetDeviceParameter(
                Settings.Project1PlcSoftwarePath0,
                parameter.Name.ToUpperInvariant(),
                wanted.ToString(),
                _backupDirectory);

            Assert.AreEqual(parameter.Name, applied.Name);
            Assert.AreEqual(wanted, ParameterCalled(parameter.Name).Value);
        }

        [TestMethod]
        public void SetDeviceParameter_TheValueItAlreadyHas_ChangesNothing()
        {
            var parameter = AWritableBoolean();
            var current = (bool)parameter.Value!;

            var applied = AssemblyHooks.SharedPortal.SetDeviceParameter(
                Settings.Project1PlcSoftwarePath0,
                parameter.Name,
                current.ToString(),
                _backupDirectory);

            Assert.AreEqual(current, applied.Value);
        }

        [TestMethod]
        public void SetDeviceParameter_AParameterThatDoesNotExist_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetDeviceParameter(
                    Settings.Project1PlcSoftwarePath0, "NoSuchParameter", "1", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// Most of what a device reports describes it rather than configures it, so this is the
        /// commonest mistake with an attribute bag. Openness answers it the same way it answers a
        /// misspelt name, which is why the refusal happens here instead.
        /// </remarks>
        [TestMethod]
        public void SetDeviceParameter_AReadOnlyParameter_IsRefusedBeforeTiaIsAsked()
        {
            var readOnly = AReadOnlyParameter();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetDeviceParameter(
                    Settings.Project1PlcSoftwarePath0, readOnly, "1", _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void SetDeviceParameter_TheBackupTaken_RecordsTheValueBeforeItChanged()
        {
            var parameter = AWritableBoolean();
            var before = (bool)parameter.Value!;

            AssemblyHooks.SharedPortal.SetDeviceParameter(
                Settings.Project1PlcSoftwarePath0,
                parameter.Name,
                (!before).ToString(),
                _backupDirectory);

            var recorded = File.ReadAllText(RecordedParametersFor(Settings.Project1PlcSoftwarePath0));

            StringAssert.Contains(recorded, $"{parameter.Name} | {before}", StringComparison.Ordinal);
        }

        [TestMethod]
        public void SetDeviceParameter_NoBackupDirectory_ThrowsInvalidParams()
        {
            var parameter = AWritableBoolean();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetDeviceParameter(
                    Settings.Project1PlcSoftwarePath0, parameter.Name, "true", string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// The debt this pair of tools was written to close. Unplugging is the one operation that
        /// puts a module's settings out of reach, so its backup has to hold them — and until
        /// parameters could be read into text, it could not.
        /// </remarks>
        [TestMethod]
        public void UnplugModule_TheBackupTaken_NowHoldsTheModulesParameters()
        {
            var slot = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0)
                .First(one => one.IsFree && one.PositionNumber > 1).PositionNumber;

            AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug("OrderNumber:6ES7 521-1BL00-0AB0/V2.1", "DI 32x24VDC HF_1", slot),
                Path.Combine(_testDirectory, "plug-backup"));

            AssemblyHooks.SharedPortal.UnplugModule("DI 32x24VDC HF_1", _backupDirectory);

            Assert.IsTrue(
                File.Exists(RecordedParametersFor("DI 32x24VDC HF_1")),
                "The removal recorded no parameters for the module it removed");
        }

        /// <remarks>
        /// The file is found rather than named. How a device path becomes a file name is the
        /// writer's business, and a test that spelled the rule again would pass while the two
        /// drifted apart.
        /// </remarks>
        private string RecordedParametersFor(string devicePath)
        {
            var directory = Path.Combine(_backupDirectory, "hardware", "parameters");

            Assert.IsTrue(Directory.Exists(directory), $"No parameters were recorded under {directory}");

            var file = Directory.GetFiles(directory, "*.txt")
                .FirstOrDefault(one => File.ReadAllText(one).Contains(devicePath));

            Assert.IsNotNull(file, $"Nothing recorded under {directory} names '{devicePath}'");

            return file;
        }

        private static ObjectAttribute AWritableBoolean()
        {
            var parameters = AssemblyHooks.SharedPortal.GetDeviceParameters(Settings.Project1PlcSoftwarePath0);
            var found = parameters.FirstOrDefault(parameter => parameter.Value is bool);

            Assert.IsNotNull(found, $"The CPU has no writable yes-or-no parameter: {Describe(parameters)}");

            return found;
        }

        private static string AReadOnlyParameter()
        {
            var everything = AssemblyHooks.SharedPortal.GetDeviceItem(Settings.Project1PlcSoftwarePath0);

            Assert.IsNotNull(everything);

            var found = everything.Attributes.FirstOrDefault(attribute => attribute.AccessMode == "Read");

            Assert.IsNotNull(found, "The CPU reports no read-only attribute, which cannot be right");

            return found.Name;
        }

        private static ObjectAttribute ParameterCalled(string name)
        {
            var parameters = AssemblyHooks.SharedPortal.GetDeviceParameters(Settings.Project1PlcSoftwarePath0);
            var found = parameters.FirstOrDefault(parameter => parameter.Name == name);

            Assert.IsNotNull(found, $"'{name}' is no longer a writable parameter");

            return found;
        }

        private static string Describe(IEnumerable<ObjectAttribute> parameters)
        {
            return string.Join("; ", parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"));
        }
    }
}
