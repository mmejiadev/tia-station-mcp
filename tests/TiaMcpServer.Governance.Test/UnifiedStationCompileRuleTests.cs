using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A Unified HMI compiles only with its whole station, which would also compile any PLC program in
    /// it behind the write policy's back. The rule refuses that; these tests are the rule's assertion.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedStationCompileRuleTests
    {
        [TestMethod]
        public void RequireNoPlcPrograms_StationHoldsAPlcProgram_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => UnifiedStationCompileRule.RequireNoPlcPrograms("HMI_RT_1", "PC-System_1", new[] { "Software PLC_1" }));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void RequireNoPlcPrograms_StationHoldsAPlcProgram_NamesItInTheRefusal()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => UnifiedStationCompileRule.RequireNoPlcPrograms("HMI_RT_1", "PC-System_1", new[] { "Software PLC_1" }));

            StringAssert.Contains(failure.Message, "'Software PLC_1'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireNoPlcPrograms_StationHoldsOnlyTheHmi_DoesNotThrow()
        {
            UnifiedStationCompileRule.RequireNoPlcPrograms("HMI_RT_1", "PC-System_1", Array.Empty<string>());
        }
    }
}
