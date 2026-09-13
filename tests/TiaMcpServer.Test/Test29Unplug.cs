using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Taking a rack apart: unplugging a module, moving one and copying one.
    /// </summary>
    /// <remarks>
    /// The destructive corner of the hardware tools, so what is asserted here is mostly what they
    /// refuse. The CPU and the built-in items are the two things in a rack that must survive a tool
    /// called "unplug a module", and neither refusal is TIA's — both are decisions taken here.
    ///
    /// Every test plugs its own card first and works on a copy of the project retrieved per test:
    /// a test that left a rack short of a module would fail somewhere else entirely.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test29Unplug
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

        [TestMethod]
        public void UnplugModule_APluggedCard_LeavesTheSlotFree()
        {
            var slot = PlugAnInputCard();

            AssemblyHooks.SharedPortal.UnplugModule(ModuleName, _backupDirectory);

            Assert.IsTrue(SlotAt(slot).IsFree, $"Slot {slot} still holds something after unplugging the card");
        }

        /// <remarks>
        /// The answer has to be enough to undo the removal without going to look for a file. Order
        /// number and slot are what PlugModule takes, so those are what it carries.
        /// </remarks>
        [TestMethod]
        public void UnplugModule_APluggedCard_ReportsWhatItWouldTakeToPutItBack()
        {
            var slot = PlugAnInputCard();

            var removed = AssemblyHooks.SharedPortal.UnplugModule(ModuleName, _backupDirectory);

            Assert.AreEqual(slot, removed.PositionNumber);
            Assert.AreEqual(DigitalInputCard, removed.TypeIdentifier);
        }

        /// <remarks>
        /// The record is only worth having if it works, so this puts the card back from it rather
        /// than asserting that it looks right.
        /// </remarks>
        [TestMethod]
        public void UnplugModule_ThenPlugModuleFromTheRecord_RestoresTheRack()
        {
            PlugAnInputCard();
            var removed = AssemblyHooks.SharedPortal.UnplugModule(ModuleName, _backupDirectory);

            AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug(removed.TypeIdentifier, ModuleName, removed.PositionNumber),
                _backupDirectory);

            Assert.IsFalse(SlotAt(removed.PositionNumber).IsFree, "The card did not come back from its own record");
        }

        /// <remarks>
        /// The refusal this class exists for. Unplugging the CPU is deleting the station's program,
        /// and no tool whose name is about modules should be able to do it by accident.
        /// </remarks>
        [TestMethod]
        public void UnplugModule_TheCpu_IsRefused()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.UnplugModule(Settings.Project1PlcSoftwarePath0, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "program", StringComparison.Ordinal);
        }

        [TestMethod]
        public void UnplugModule_ABuiltInItem_IsRefused()
        {
            var builtIn = AssemblyHooks.SharedPortal.GetNetworkTopology()
                .First(node => node.DevicePath.StartsWith("PLC_0/", StringComparison.Ordinal));

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.UnplugModule(builtIn.DevicePath, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void UnplugModule_NoBackupDirectory_ThrowsInvalidParams()
        {
            PlugAnInputCard();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.UnplugModule(ModuleName, string.Empty));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void MoveModule_AFreeSlot_TakesTheModuleThere()
        {
            var slot = PlugAnInputCard();
            var target = FirstFreeSlotForAnIoCard();

            AssemblyHooks.SharedPortal.MoveModule(ModuleName, target, _backupDirectory);

            Assert.IsTrue(SlotAt(slot).IsFree, $"Slot {slot} still holds the card after it moved");
            Assert.IsFalse(SlotAt(target).IsFree, $"Slot {target} is empty after the card moved into it");
        }

        [TestMethod]
        public void MoveModule_TheSlotItIsAlreadyIn_ChangesNothing()
        {
            var slot = PlugAnInputCard();

            AssemblyHooks.SharedPortal.MoveModule(ModuleName, slot, _backupDirectory);

            Assert.IsFalse(SlotAt(slot).IsFree, "The card left the slot it was asked to stay in");
        }

        /// <remarks>
        /// Nothing displaces anything, here as everywhere else in this repository: a move onto an
        /// occupied slot is refused with the occupant named, rather than swapping the two.
        /// </remarks>
        [TestMethod]
        public void MoveModule_OntoTheCpu_IsRefusedAndNamesIt()
        {
            PlugAnInputCard();
            var cpuSlot = OccupiedSlotOfTheCpu();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.MoveModule(ModuleName, cpuSlot, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, Settings.Project1PlcSoftwarePath0, StringComparison.Ordinal);
        }

        [TestMethod]
        public void CopyModule_AFreeSlot_LeavesBothCardsInPlace()
        {
            var original = PlugAnInputCard();
            var target = FirstFreeSlotForAnIoCard();

            var copy = AssemblyHooks.SharedPortal.CopyModule(ModuleName, target, _backupDirectory);

            Assert.IsFalse(SlotAt(original).IsFree, "The original is gone: that was a move, not a copy");
            Assert.AreEqual(copy, SlotAt(target).OccupantName);
        }

        /// <remarks>
        /// TIA names the copy itself, appending a number, so the name is read back rather than
        /// chosen. A caller that assumed its own name would be addressing something that does not
        /// exist.
        /// </remarks>
        [TestMethod]
        public void CopyModule_AFreeSlot_ReturnsANameThatIsNotTheOriginals()
        {
            PlugAnInputCard();
            var target = FirstFreeSlotForAnIoCard();

            var copy = AssemblyHooks.SharedPortal.CopyModule(ModuleName, target, _backupDirectory);

            Assert.AreNotEqual(ModuleName, copy, "The copy carries the original's name");
        }

        [TestMethod]
        public void CopyModule_OntoAnOccupiedSlot_IsRefused()
        {
            PlugAnInputCard();
            var cpuSlot = OccupiedSlotOfTheCpu();

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CopyModule(ModuleName, cpuSlot, _backupDirectory));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        /// <summary>Plugs the card these tests take apart, and returns the slot it went into.</summary>
        private int PlugAnInputCard()
        {
            var slot = FirstFreeSlotForAnIoCard();

            AssemblyHooks.SharedPortal.PlugModule(
                Settings.Project1PlcSoftwarePath0,
                new ModuleToPlug(DigitalInputCard, ModuleName, slot),
                Path.Combine(_testDirectory, "plug-backup"));

            return slot;
        }

        private static int OccupiedSlotOfTheCpu()
        {
            return AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0)
                .First(slot => !slot.IsFree && slot.OccupantName == Settings.Project1PlcSoftwarePath0)
                .PositionNumber;
        }

        private static int FirstFreeSlotForAnIoCard()
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);

            var cpu = slots.First(slot => !slot.IsFree);

            return slots.First(slot => slot.IsFree && slot.PositionNumber > cpu.PositionNumber).PositionNumber;
        }

        private static PlugLocationInfo SlotAt(int positionNumber)
        {
            var slots = AssemblyHooks.SharedPortal.GetPlugLocations(Settings.Project1PlcSoftwarePath0);
            var slot = slots.FirstOrDefault(one => one.PositionNumber == positionNumber);

            Assert.IsNotNull(slot, $"Slot {positionNumber} is no longer in the rack");

            return slot;
        }
    }
}
