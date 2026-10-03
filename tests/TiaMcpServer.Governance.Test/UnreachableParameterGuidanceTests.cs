using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class UnreachableParameterGuidanceTests
    {
        [TestMethod]
        [DataRow("ClockMemoryByte")]
        [DataRow("ClockMemoryByteAddress")]
        [DataRow("SystemMemoryByte")]
        [DataRow("SystemMemoryByteAddress")]
        public void TryFind_TheSystemAndClockMemory_SaysWhereToTickItByHand(string parameterName)
        {
            var isKnown = UnreachableParameterGuidance.TryFind(parameterName, out var guidance);

            Assert.IsTrue(isKnown);
            StringAssert.Contains(guidance, "General > System and clock memory", StringComparison.Ordinal);
        }

        [TestMethod]
        public void TryFind_ANameInOtherCase_IsStillKnown()
        {
            var isKnown = UnreachableParameterGuidance.TryFind("clockmemorybyte", out _);

            Assert.IsTrue(isKnown);
        }

        [TestMethod]
        public void TryFind_AnyOtherParameter_HasNoGuidance()
        {
            var isKnown = UnreachableParameterGuidance.TryFind("ProtectionLevel", out var guidance);

            Assert.IsFalse(isKnown);
            Assert.AreEqual(string.Empty, guidance);
        }

        [TestMethod]
        public void TryFind_NoName_HasNoGuidance()
        {
            var isKnown = UnreachableParameterGuidance.TryFind(null!, out _);

            Assert.IsFalse(isKnown);
        }
    }
}
