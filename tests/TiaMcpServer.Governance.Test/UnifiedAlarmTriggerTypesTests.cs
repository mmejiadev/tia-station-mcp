using TiaMcpServer.UnifiedAlarms;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// One alarm of each kind on a tag of each type, compiled on TIA Portal V20 (measured 2026-10-25):
    /// every pairing refused here compiled as "The data type of the tag is not permitted for this type of
    /// alarm", and every bit refused here was refused by Openness as "The trigger bit number is invalid".
    /// </remarks>
    [TestClass]
    public sealed class UnifiedAlarmTriggerTypesTests
    {
        [TestMethod]
        [DataRow("Bool", 0u)]
        [DataRow("Byte", 7u)]
        [DataRow("Word", 15u)]
        [DataRow("DWord", 31u)]
        [DataRow("LWord", 63u)]
        public void DiscreteProblem_BitStringAtItsTopBit_IsNone(string dataType, uint bit)
        {
            Assert.IsNull(UnifiedAlarmTriggerTypes.DiscreteProblem(dataType, bit));
        }

        [TestMethod]
        [DataRow("Bool", 1u, DisplayName = "Bool bit 1")]
        [DataRow("Byte", 8u, DisplayName = "Byte bit 8")]
        [DataRow("Word", 16u, DisplayName = "Word bit 16")]
        [DataRow("DWord", 32u, DisplayName = "DWord bit 32")]
        [DataRow("LWord", 64u, DisplayName = "LWord bit 64")]
        [DataRow("Int", 0u, DisplayName = "an Int, even a PLC one")]
        [DataRow("UInt", 0u, DisplayName = "a UInt")]
        [DataRow("DInt", 0u, DisplayName = "a DInt")]
        [DataRow("Real", 0u, DisplayName = "a Real")]
        [DataRow("WString", 0u, DisplayName = "a WString")]
        public void DiscreteProblem_TagOrBitTheAlarmDoesNotTake_IsNamed(string dataType, uint bit)
        {
            Assert.IsNotNull(UnifiedAlarmTriggerTypes.DiscreteProblem(dataType, bit));
        }

        [TestMethod]
        [DataRow("SInt")]
        [DataRow("USInt")]
        [DataRow("Int")]
        [DataRow("UInt")]
        [DataRow("DInt")]
        [DataRow("UDInt")]
        [DataRow("LInt")]
        [DataRow("ULInt")]
        [DataRow("Real")]
        [DataRow("LReal")]
        public void AnalogProblem_Number_IsNone(string dataType)
        {
            Assert.IsNull(UnifiedAlarmTriggerTypes.AnalogProblem(dataType));
        }

        [TestMethod]
        [DataRow("LInt", "9007199254740993", DisplayName = "an LInt one past 2^53")]
        [DataRow("ULInt", "18446744073709551615", DisplayName = "the largest ULInt")]
        [DataRow("Int", "99999", DisplayName = "outside an Int")]
        [DataRow("Int", "1.5", DisplayName = "a fraction for an Int")]
        public void AnalogValueProblem_ValueTiaPortalCannotKeep_IsNamed(string dataType, string value)
        {
            Assert.IsNotNull(UnifiedAlarmTriggerTypes.AnalogValueProblem(dataType, value, "A"));
        }

        [TestMethod]
        [DataRow("LInt", "9007199254740992", DisplayName = "an LInt at 2^53")]
        [DataRow("Real", "8.5", DisplayName = "a Real")]
        [DataRow("Int", "-5", DisplayName = "a negative Int")]
        public void AnalogValueProblem_ValueThatFits_IsNone(string dataType, string value)
        {
            Assert.IsNull(UnifiedAlarmTriggerTypes.AnalogValueProblem(dataType, value, "A"));
        }

        [TestMethod]
        [DataRow("Bool")]
        [DataRow("Byte")]
        [DataRow("Word")]
        [DataRow("DWord")]
        [DataRow("LWord")]
        [DataRow("WString")]
        public void AnalogProblem_NotANumber_IsNamed(string dataType)
        {
            Assert.IsNotNull(UnifiedAlarmTriggerTypes.AnalogProblem(dataType));
        }
    }
}
