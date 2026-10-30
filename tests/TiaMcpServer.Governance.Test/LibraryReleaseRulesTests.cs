using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-10-30 on TIA Portal V20: Release takes a repeated number and keeps two versions under it.</remarks>
    [TestClass]
    public sealed class LibraryReleaseRulesTests
    {
        private static readonly Version V001 = new Version(0, 0, 1);

        private static readonly Version V002 = new Version(0, 0, 2);

        [TestMethod]
        public void Choose_NoNumberGiven_KeepsTheVersionInWorksOwn()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: true), new LibraryVersionInfo(V002, isReleased: false) };

            var number = LibraryReleaseRules.Choose("FP_Motor", versions, null);

            Assert.AreEqual(V002, number);
        }

        [TestMethod]
        public void Choose_ANumberNotHigherThanEveryReleasedOne_IsRefused()
        {
            var versions = new[] { new LibraryVersionInfo(V002, isReleased: true), new LibraryVersionInfo(new Version(0, 0, 3), isReleased: false) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryReleaseRules.Choose("FP_Motor", versions, V001));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "0.0.2 is already released", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Choose_TheSameNumberAsAReleasedOne_IsRefused()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: true), new LibraryVersionInfo(V002, isReleased: false) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryReleaseRules.Choose("FP_Motor", versions, V001));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Choose_NoVersionInWork_IsRefusedAsInvalidState()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: true) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryReleaseRules.Choose("FP_Motor", versions, null));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "nothing to release", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Choose_TwoVersionsInWork_IsRefusedAsInvalidState()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: false), new LibraryVersionInfo(V002, isReleased: false) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryReleaseRules.Choose("FP_Motor", versions, null));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        /// <remarks>The review of 2026-10-30: Version equality counts parts, and what Openness fills in was not measured.</remarks>
        [TestMethod]
        public void Choose_AReleasedNumberReadInFourParts_StillRefusesTheSameNumber()
        {
            var versions = new[] { new LibraryVersionInfo(new Version(0, 0, 1, 0), isReleased: true), new LibraryVersionInfo(V002, isReleased: false) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryReleaseRules.Choose("FP_Motor", versions, V001));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void RequireDependenciesReleased_ADependencyInWork_IsRefusedNamingIt()
        {
            var dependencies = new[] { ("UDT_Motor", false), ("UDT_Valve", true) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryReleaseRules.RequireDependenciesReleased("FP_Motor", dependencies));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "'UDT_Motor', still in work", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireDependenciesReleased_EveryDependencyReleased_PassesSilently()
        {
            LibraryReleaseRules.RequireDependenciesReleased("FP_Motor", new[] { ("UDT_Motor", true) });
        }

        [TestMethod]
        public void Choose_AFirstReleaseWithAnyNumber_IsTaken()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: false) };

            var number = LibraryReleaseRules.Choose("FP_Motor", versions, new Version(1, 0, 0));

            Assert.AreEqual(new Version(1, 0, 0), number);
        }
    }
}
