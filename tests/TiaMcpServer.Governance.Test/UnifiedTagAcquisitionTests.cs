using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The cycles are named objects: Openness takes any name and the compile says it does not exist. These
    /// are the seven that compiled out of 24 tried on TIA Portal V20 (measured 2026-10-25).
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTagAcquisitionTests
    {
        private static readonly string[] MeasuredCycles = { "T100ms", "T250ms", "T500ms", "T1s", "T2s", "T5s", "T10s" };

        [TestMethod]
        public void Cycles_AreTheSevenThatCompiled()
        {
            CollectionAssert.AreEqual(MeasuredCycles, (System.Collections.ICollection)UnifiedTagAcquisition.Cycles);
        }

        [TestMethod]
        public void NormaliseCycle_OtherCase_IsSpelledAsTiaPortalDoes()
        {
            Assert.AreEqual("T1s", UnifiedTagAcquisition.NormaliseCycle(" t1S ", "T"));
        }

        [TestMethod]
        [DataRow("T10ms", DisplayName = "faster than any cycle there is")]
        [DataRow("T1min", DisplayName = "a minute: 'The object does not exist'")]
        [DataRow("100 ms", DisplayName = "a duration rather than a name")]
        [DataRow("", DisplayName = "empty: 'The property Acquisition cycle is invalid'")]
        public void NormaliseCycle_NameTheCompileRefuses_ThrowsInvalidParams(string cycle)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTagAcquisition.NormaliseCycle(cycle, "T"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
