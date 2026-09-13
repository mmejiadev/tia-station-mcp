using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Where a module's data lands in the process image: reading the ranges of a rack and moving
    /// one of them.
    /// </summary>
    /// <remarks>
    /// These change the project, so each test works on a copy retrieved per test. Every one of them
    /// plugs its own input card first: the fixture rack holds a CPU and nothing that occupies an
    /// address, which is itself the reason this pair of tools exists — a card is plugged and then
    /// has to be told where it lives.
    ///
    /// <see cref="GetIoAddresses_AnInputCard_CountsItsLengthInBits"/> is the measurement. Openness
    /// gives <c>Address.Length</c> as a number with no unit anywhere in the signature, and reading
    /// bits as bytes would understate every range eightfold — a 32-channel card would look like
    /// four channels and the span printed beside it would be wrong.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test28Addresses
    {
        private const string DigitalInputCard = "OrderNumber:6ES7 521-1BL00-0AB0/V2.1";
        private const string ModuleName = "DI 32x24VDC HF_1";

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

        /// <remarks>
        /// A 32-channel input card occupies four bytes. If this reports 4 rather than 32 the unit
        /// is bytes, every span this server prints is eight times too short, and the constructor
        /// doc of IoAddressInfo is wrong rather than the card being unusual.
        /// </remarks>
        [TestMethod]
        public void GetIoAddresses_AnInputCard_CountsItsLengthInBits()
        {
            var card = PlugAnInputCard();

            var input = InputRangeOf(card);

            Assert.AreEqual(32, input.LengthInBits, "A 32-channel card reports a length that is not 32 bits");
        }

        [TestMethod]
        public void GetIoAddresses_AnInputCard_RendersItsSpanAsAProgramWouldWriteIt()
        {
            var card = PlugAnInputCard();

            var input = InputRangeOf(card);

            Assert.AreEqual($"%I{input.StartAddress}.0..%I{input.StartAddress + 3}.7", input.Span);
        }

        [TestMethod]
        public void GetIoAddresses_ARackWithNothingPluggedIn_ReportsNoRangeForTheCpu()
        {
            var ranges = AssemblyHooks.SharedPortal.GetIoAddresses(Settings.Project1PlcSoftwarePath0);

            Assert.IsFalse(
                ranges.Any(range => range.IoType == "Input" && range.ModulePath.Contains("DI ")),
                $"The fixture already holds an input card: {Describe(ranges)}");
        }

        [TestMethod]
        public void GetIoAddresses_AnUnknownPath_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetIoAddresses("NoSuchDevice"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// The round trip: the module path comes out of the read tool and goes straight into the
        /// write. A path only the read can produce and the write cannot resolve is the defect this
        /// repository has now found twice, in subnets and in device paths.
        /// </remarks>
        [TestMethod]
        public void SetModuleAddress_APathFromTheRead_MovesTheRangeThereAndTheReadShowsIt()
        {
            var card = PlugAnInputCard();

            var moved = AssemblyHooks.SharedPortal.SetModuleAddress(card, "Input", 64, _backupDirectory);

            Assert.AreEqual(64, moved.StartAddress);
            Assert.AreEqual(64, InputRangeOf(card).StartAddress);
        }

        [TestMethod]
        public void SetModuleAddress_TheAddressItAlreadyHas_ChangesNothing()
        {
            var card = PlugAnInputCard();
            var start = InputRangeOf(card).StartAddress;

            var moved = AssemblyHooks.SharedPortal.SetModuleAddress(card, "Input", start, _backupDirectory);

            Assert.AreEqual(start, moved.StartAddress);
        }

        [TestMethod]
        public void SetModuleAddress_AnOutputRangeOnAnInputCard_SaysWhatItHasInstead()
        {
            var card = PlugAnInputCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetModuleAddress(card, "Output", 0, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "It has:", StringComparison.Ordinal);
        }

        [TestMethod]
        public void SetModuleAddress_ARangeThatIsNotInputOrOutput_ThrowsInvalidParams()
        {
            var card = PlugAnInputCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetModuleAddress(card, "Diagnosis", 0, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void SetModuleAddress_ANegativeAddress_ThrowsInvalidParams()
        {
            var card = PlugAnInputCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetModuleAddress(card, "Input", -1, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// The backup has to contain what is about to change, which for this write is the address
        /// itself. A module layout recording slots and order numbers but not addresses would be a
        /// record of everything except the thing being moved.
        /// </remarks>
        [TestMethod]
        public void SetModuleAddress_TheBackupTaken_RecordsTheAddressBeforeItMoved()
        {
            var card = PlugAnInputCard();
            var before = InputRangeOf(card);

            AssemblyHooks.SharedPortal.SetModuleAddress(card, "Input", 64, _backupDirectory);

            var recorded = File.ReadAllText(Path.Combine(_backupDirectory, "hardware", "modules.txt"));

            StringAssert.Contains(recorded, before.Span, StringComparison.Ordinal);
        }

        [TestMethod]
        public void SetModuleAddress_NoBackupDirectory_ThrowsInvalidParams()
        {
            var card = PlugAnInputCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.SetModuleAddress(card, "Input", 64, string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <summary>Plugs the card these tests move about, and returns the path it answers to.</summary>
        private string PlugAnInputCard()
        {
            var slot = FirstFreeSlotForAnIoCard();

            AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug(DigitalInputCard, ModuleName, slot),
                Path.Combine(_testDirectory, "plug-backup"));

            var ranges = AssemblyHooks.SharedPortal.GetIoAddresses(Settings.Project1PlcSoftwarePath0);
            var card = ranges.FirstOrDefault(range => range.ModulePath.EndsWith(ModuleName, StringComparison.Ordinal));

            Assert.IsNotNull(card, $"The card was plugged and occupies no address: {Describe(ranges)}");

            return card.ModulePath;
        }

        private static IoAddressInfo InputRangeOf(string modulePath)
        {
            var ranges = AssemblyHooks.SharedPortal.GetIoAddresses(Settings.Project1PlcSoftwarePath0);
            var input = ranges.FirstOrDefault(range => range.ModulePath == modulePath && range.IoType == "Input");

            Assert.IsNotNull(input, $"'{modulePath}' has no input range: {Describe(ranges)}");

            return input;
        }

        private static int FirstFreeSlotForAnIoCard()
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);

            var cpu = slots.First(slot => !slot.IsFree);

            return slots.First(slot => slot.IsFree && slot.PositionNumber > cpu.PositionNumber).PositionNumber;
        }

        private static string Describe(IEnumerable<IoAddressInfo> ranges)
        {
            return string.Join("; ", ranges.Select(range => $"{range.ModulePath}|{range.IoType}|{range.Span}"));
        }
    }
}
