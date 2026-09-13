using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Everything asserted here is refused before TIA Portal is asked anything, which is the point:
    /// Openness answers a negative slot, an empty name and a rack that is full with the same
    /// generic failure, and the three have nothing to do with each other.
    /// </remarks>
    [TestClass]
    public sealed class ModuleToPlugTests
    {
        private const string SomeOrderNumber = "OrderNumber:6ES7 521-1BL00-0AB0/V2.1";

        [TestMethod]
        public void Constructor_AllThreeGiven_KeepsThem()
        {
            var module = new ModuleToPlug(SomeOrderNumber, "DI_1", 2);

            Assert.AreEqual(SomeOrderNumber, module.TypeIdentifier);
            Assert.AreEqual("DI_1", module.Name);
            Assert.AreEqual(2, module.PositionNumber);
        }

        [TestMethod]
        public void Constructor_SurroundingSpace_IsTrimmed()
        {
            var module = new ModuleToPlug("  " + SomeOrderNumber + "  ", "  DI_1  ", 2);

            Assert.AreEqual(SomeOrderNumber, module.TypeIdentifier);
            Assert.AreEqual("DI_1", module.Name);
        }

        [TestMethod]
        public void Constructor_NoTypeIdentifier_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => new ModuleToPlug(" ", "DI_1", 2));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Constructor_NoName_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => new ModuleToPlug(SomeOrderNumber, " ", 2));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// Slot zero exists on some racks, so only a negative slot is refused here. Refusing zero
        /// as well would be this server inventing a rule of its own about hardware it cannot see.
        /// </remarks>
        [TestMethod]
        public void Constructor_ANegativeSlot_ThrowsInvalidParamsAndPointsAtTheRead()
        {
            var failure = Assert.ThrowsException<PortalException>(() => new ModuleToPlug(SomeOrderNumber, "DI_1", -1));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "GetPlugLocations", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Constructor_SlotZero_IsAccepted()
        {
            var module = new ModuleToPlug(SomeOrderNumber, "PS_1", 0);

            Assert.AreEqual(0, module.PositionNumber);
        }
    }
}
