using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Which version of a library type may be discarded, made the default or deleted. A deleted released version is gone:
    /// the type it held cannot be exported, so a refusal here is what keeps a faceplate drawn by hand from being lost to a
    /// version number written wrong.
    /// </remarks>
    [TestClass]
    public sealed class LibraryVersionRulesTests
    {
        private static readonly Version V1 = new Version(0, 0, 1);
        private static readonly Version V2 = new Version(0, 0, 2);
        private static readonly Version V3 = new Version(0, 0, 3);

        private static LibraryVersionInfo[] ThreeVersions()
        {
            return new[]
            {
                new LibraryVersionInfo(V1, true),
                new LibraryVersionInfo(V2, true, true),
                new LibraryVersionInfo(V3, false)
            };
        }

        [TestMethod]
        public void RequireDiscardable_OneInWork_ReturnsIt()
        {
            var number = LibraryVersionRules.RequireDiscardable("FP_Motor", ThreeVersions());

            Assert.AreEqual(V3, number);
        }

        [TestMethod]
        public void RequireDiscardable_NoneInWork_ThrowsInvalidState()
        {
            var versions = new[] { new LibraryVersionInfo(V1, true, true) };

            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDiscardable("FP_Motor", versions));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void RemovesTheType_TheOnlyVersionInWork_IsTrue()
        {
            var versions = new[] { new LibraryVersionInfo(V1, false) };

            Assert.IsTrue(LibraryVersionRules.RemovesTheType(versions));
        }

        [TestMethod]
        public void RemovesTheType_ReleasedVersionsBeside_IsFalse()
        {
            Assert.IsFalse(LibraryVersionRules.RemovesTheType(ThreeVersions()));
        }

        [TestMethod]
        public void RequireTypeDeletable_AVersionInWork_ThrowsInvalidStateNamingDiscard()
        {
            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireTypeDeletable("FP_Motor", ThreeVersions()));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "DiscardLibraryTypeEdit", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireTypeDeletable_AllReleased_Passes()
        {
            LibraryVersionRules.RequireTypeDeletable("FP_Motor", new[] { new LibraryVersionInfo(V1, true), new LibraryVersionInfo(V2, true, true) });
        }

        [TestMethod]
        public void RequireDefaultable_AReleasedVersion_ReturnsIt()
        {
            var number = LibraryVersionRules.RequireDefaultable("FP_Motor", ThreeVersions(), V1);

            Assert.AreEqual(V1, number);
        }

        [TestMethod]
        public void RequireDefaultable_TheVersionInWork_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDefaultable("FP_Motor", ThreeVersions(), V3));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void RequireDefaultable_AVersionNotThere_ThrowsInvalidParamsNamingThoseThatAre()
        {
            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDefaultable("FP_Motor", ThreeVersions(), new Version(0, 0, 9)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "0.0.2 (default)", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireDeletable_AReleasedVersionNotTheDefault_ReturnsIt()
        {
            var number = LibraryVersionRules.RequireDeletable("FP_Motor", ThreeVersions(), V1);

            Assert.AreEqual(V1, number);
        }

        [TestMethod]
        public void RequireDeletable_TheDefault_ThrowsInvalidStateNamingTheWayOut()
        {
            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDeletable("FP_Motor", ThreeVersions(), V2));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "SetDefaultLibraryTypeVersion", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireDeletable_TheVersionInWork_ThrowsInvalidStateNamingDiscard()
        {
            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDeletable("FP_Motor", ThreeVersions(), V3));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "DiscardLibraryTypeEdit", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireDeletable_TheOnlyVersion_ThrowsInvalidStateNamingDeleteType()
        {
            var versions = new[] { new LibraryVersionInfo(V1, true, true) };

            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDeletable("FP_Motor", versions, V1));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "DeleteLibraryType", StringComparison.Ordinal);
        }

        /// <remarks>Review of 2026-11-16: "make another one the default" named nothing that could be made one.</remarks>
        [TestMethod]
        public void RequireDeletable_TheOnlyReleasedBesideOneInWork_ThrowsInvalidStateNamingRelease()
        {
            var versions = new[] { new LibraryVersionInfo(V1, true, true), new LibraryVersionInfo(V2, false, false) };

            var failure = Assert.ThrowsException<PortalException>(() => LibraryVersionRules.RequireDeletable("FP_Motor", versions, V1));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "ReleaseLibraryType", StringComparison.Ordinal);
        }
    }
}
