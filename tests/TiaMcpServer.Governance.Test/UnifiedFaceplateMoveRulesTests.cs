using System;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The review of 2026-11-02: a version dropping a property moved an instance and reported success, its tag gone.</remarks>
    [TestClass]
    public sealed class UnifiedFaceplateMoveRulesTests
    {
        private const string Reference = "V0.0.2\\FP_Motor";

        private static readonly (string Name, string Value)[] Motor = { ("Motor", "M2") };

        private static readonly (string Name, string Value)[] Renamed = { ("Motor2", "M2") };

        private static readonly (string Name, string Value)[] Other = { ("Motor", "M3") };

        private static readonly (string Name, string Value)[] Empty = { ("Motor", string.Empty) };

        private static readonly (string Name, string Value)[] MotorAndNew = { ("Motor", "M2"), ("Nom", string.Empty) };

        [TestMethod]
        public void CheckKept_APropertyRenamed_IsRefusedNamingIt()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateMoveRules.CheckKept("FP_M2", Reference, Motor, Renamed));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "'Motor' (held 'M2')", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CheckKept_AValueChanged_IsRefused()
        {
            Assert.ThrowsException<PortalException>(() => UnifiedFaceplateMoveRules.CheckKept("FP_M2", Reference, Motor, Other));
        }

        [TestMethod]
        public void CheckKept_APropertyAdded_PassesSilently()
        {
            UnifiedFaceplateMoveRules.CheckKept("FP_M2", Reference, Motor, MotorAndNew);
        }

        [TestMethod]
        public void CheckKept_APropertyThatHeldNothingGone_PassesSilently()
        {
            UnifiedFaceplateMoveRules.CheckKept("FP_M2", Reference, Empty, Renamed);
        }
    }
}
