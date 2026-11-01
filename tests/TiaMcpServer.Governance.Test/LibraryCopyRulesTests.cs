using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-10-30 on TIA Portal V20: copying a type the project holds renumbers its version in work without a word.</remarks>
    [TestClass]
    public sealed class LibraryCopyRulesTests
    {
        private static readonly string[] InGlobal = { "FP_Motor", "UDT_Motor" };

        private static readonly string[] FaceplateAndItsType = { "FP_Motor", "UDT_Motor" };

        [TestMethod]
        public void CheckNotPresent_ATypeTheProjectAlreadyHolds_IsRefusedAsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryCopyRules.CheckNotPresent(new[] { "FP_Motor" }, new[] { "fp_motor" }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "renumber", StringComparison.Ordinal);
        }

        /// <remarks>The review of 2026-10-30 found the dependency unchecked: it would be merged as silently as a type asked for.</remarks>
        [TestMethod]
        public void CheckNotPresent_ATypeOnlyDependedOnThatTheProjectHolds_IsRefusedNamingIt()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryCopyRules.CheckNotPresent(FaceplateAndItsType, new[] { "UDT_Motor" }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "'UDT_Motor'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CheckNotPresent_NothingThere_PassesSilently()
        {
            LibraryCopyRules.CheckNotPresent(FaceplateAndItsType, new[] { "Other" });
        }

        [TestMethod]
        public void CheckRequested_ATypeTheGlobalLibraryLacks_IsRefusedNamingWhatItHolds()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryCopyRules.CheckRequested(new[] { "FP_Valve" }, InGlobal));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "FP_Motor, UDT_Motor", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CheckRequested_ANameInAnotherCase_IsFound()
        {
            LibraryCopyRules.CheckRequested(new[] { "fp_motor" }, InGlobal);
        }

        [TestMethod]
        public void CheckRequested_NoTypeNamed_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryCopyRules.CheckRequested(Array.Empty<string>(), InGlobal));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void CheckRequested_ATypeNamedTwice_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryCopyRules.CheckRequested(new[] { "FP_Motor", "fp_motor" }, InGlobal));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }
    }
}
