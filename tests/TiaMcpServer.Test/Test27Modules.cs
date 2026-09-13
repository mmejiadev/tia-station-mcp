using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Building the station: reading the slots of a rack and plugging a module into one.
    /// </summary>
    /// <remarks>
    /// These change what a station is made of, so each test works on a copy retrieved per test
    /// rather than on the shared fixture.
    ///
    /// The order number below is a standard S7-1500 input card. If TIA Portal on this machine does
    /// not carry it in its catalogue these tests fail at the plug, and the failure says which slots
    /// were free -- which is the difference between "the catalogue is different here" and "the
    /// rack refused it", and the reason the refusal names them.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test27Modules
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
        /// The CPU is in the rack it names, so the slots around it include the one it occupies.
        /// A read that returned only free slots could not tell a full rack from a rack that
        /// refuses everything.
        /// </remarks>
        [TestMethod]
        public void GetPlugLocations_ThePlc_ShowsTheSlotTheCpuOccupies()
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(slots.Count > 0, "The CPU sits in a rack and the rack reported no slots at all");
            Assert.IsTrue(
                slots.Any(slot => !slot.IsFree && slot.OccupantTypeIdentifier.Length > 0),
                $"No occupied slot carries an order number: {Describe(slots)}");
        }

        [TestMethod]
        public void GetPlugLocations_ThePlc_OffersSomewhereToPlugAModule()
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(slots.Any(slot => slot.IsFree), $"The rack has no free slot: {Describe(slots)}");
        }

        [TestMethod]
        public void GetPlugLocations_AnUnknownPath_ThrowsNotFound()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetPlugLocations("NoSuchDevice"));

            Assert.AreEqual(PortalErrorCode.NotFound, failure.Code);
        }

        /// <remarks>
        /// The round trip this repository asks of every write: the slot comes from the read tool,
        /// and the read tool is where the result shows up.
        /// </remarks>
        [TestMethod]
        public void PlugModule_AFreeSlot_IsOccupiedByItAfterwards()
        {
            var slot = FirstFreeSlotForAnIoCard();

            var plugged = AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug(DigitalInputCard, ModuleName, slot),
                _backupDirectory);

            var after = SlotAt(slot);

            Assert.AreEqual(plugged, after.OccupantName);
            Assert.IsFalse(after.IsFree, $"Slot {slot} still reads as free after plugging {plugged} into it");
        }

        [TestMethod]
        public void PlugModule_TheSameModuleTwice_ReportsTheOneThatIsThere()
        {
            var slot = FirstFreeSlotForAnIoCard();
            var module = new ModuleToPlug(DigitalInputCard, ModuleName, slot);

            var first = AssemblyHooks.SharedPortal.PlugModule(Settings.Project1PlcSoftwarePath0, module, _backupDirectory);
            var again = AssemblyHooks.SharedPortal.PlugModule(Settings.Project1PlcSoftwarePath0, module, _backupDirectory);

            Assert.AreEqual(first, again);
        }

        /// <remarks>
        /// Never overwrite. A slot holding something else is refused with both names, because
        /// replacing a module would discard its parameters and its addresses with it.
        /// </remarks>
        [TestMethod]
        public void PlugModule_ASlotHoldingSomethingElse_IsRefused()
        {
            var occupied = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0)
                .First(slot => !slot.IsFree);

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.PlugModule(
                    Settings.Project1PlcSoftwarePath0,
                    new ModuleToPlug(DigitalInputCard, ModuleName, occupied.PositionNumber),
                    _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, occupied.OccupantName, StringComparison.Ordinal);
        }

        /// <remarks>
        /// An order number TIA does not know and a slot that will not take the module are the same
        /// refusal from Openness, so the message names the free slots rather than guessing which
        /// of the two it was.
        /// </remarks>
        [TestMethod]
        public void PlugModule_AnOrderNumberTiaDoesNotKnow_SaysWhichSlotsAreFree()
        {
            var slot = FirstFreeSlotForAnIoCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.PlugModule(
                    Settings.Project1PlcSoftwarePath0,
                    new ModuleToPlug("OrderNumber:6ES7 000-0AA00-0AA0/V1.0", ModuleName, slot),
                    _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "Free slots:", StringComparison.Ordinal);
        }

        [TestMethod]
        public void PlugModule_RecordsTheModuleLayoutBeforeChangingIt()
        {
            var slot = FirstFreeSlotForAnIoCard();

            AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug(DigitalInputCard, ModuleName, slot),
                _backupDirectory);

            Assert.IsTrue(
                File.Exists(Path.Combine(_backupDirectory, "hardware", "modules.txt")),
                $"No hardware backup was written to {_backupDirectory}");
        }

        /// <remarks>
        /// The backup has to contain the thing that is about to change. A file recording the
        /// network table instead would be a receipt for a change it does not describe -- which is
        /// what a hardware write would have produced had it reused the network backup.
        /// </remarks>
        [TestMethod]
        public void PlugModule_TheBackupTaken_DescribesTheRackBeforeTheModuleWentIn()
        {
            var slot = FirstFreeSlotForAnIoCard();

            AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug(DigitalInputCard, ModuleName, slot),
                _backupDirectory);

            var recorded = File.ReadAllText(Path.Combine(_backupDirectory, "hardware", "modules.txt"));

            StringAssert.Contains(recorded, Settings.Project1PlcSoftwarePath0, StringComparison.Ordinal);
            Assert.IsFalse(
                recorded.Contains(ModuleName),
                "The backup already contains the module that was plugged after it was taken");
        }

        [TestMethod]
        public void PlugModule_NoBackupDirectory_ThrowsInvalidParams()
        {
            var slot = FirstFreeSlotForAnIoCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.PlugModule(
                    Settings.Project1PlcSoftwarePath0,
                    new ModuleToPlug(DigitalInputCard, ModuleName, slot),
                    string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// The first free slot **after the CPU**, not the first free slot. Measured on 2026-09-13:
        /// slot 0 of an S7-1500 rack reads as free and refuses an input card, because it is the
        /// power supply's. That is not a defect of the read — a free slot genuinely is not the same
        /// as a slot that accepts a given module, which is why the refusal names both — but it does
        /// mean a test that wants somewhere to put an IO card has to ask for one.
        /// </remarks>
        private static int FirstFreeSlotForAnIoCard()
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);

            var cpu = slots.FirstOrDefault(slot => !slot.IsFree);
            Assert.IsNotNull(cpu, $"The rack reports nothing plugged into it at all: {Describe(slots)}");

            var free = slots.FirstOrDefault(slot => slot.IsFree && slot.PositionNumber > cpu.PositionNumber);
            Assert.IsNotNull(free, $"No free slot after the CPU: {Describe(slots)}");

            return free.PositionNumber;
        }

        private static PlugLocationInfo SlotAt(int positionNumber)
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);
            var slot = slots.FirstOrDefault(one => one.PositionNumber == positionNumber);

            Assert.IsNotNull(slot, $"Slot {positionNumber} is no longer in the rack: {Describe(slots)}");

            return slot;
        }

        private static string Describe(IEnumerable<PlugLocationInfo> slots)
        {
            return string.Join("; ", slots.Select(slot => $"{slot.PositionNumber}|{(slot.IsFree ? "free" : slot.OccupantName)}|{slot.OccupantTypeIdentifier}"));
        }
    }
}
