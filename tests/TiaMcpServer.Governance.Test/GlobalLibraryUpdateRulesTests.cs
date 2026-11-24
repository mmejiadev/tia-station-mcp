using System;
using System.Linq;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// New versions of a type a global library already holds. A global library is often a class's shared one, so what is refused
    /// here is what was not measured: a global library whose versions went another way than the project's.
    /// </remarks>
    [TestClass]
    public sealed class GlobalLibraryUpdateRulesTests
    {
        private static readonly Version V1 = new Version(0, 0, 1);
        private static readonly Version V2 = new Version(0, 0, 2);
        private static readonly Version V3 = new Version(0, 0, 3);
        private static readonly Guid Id1 = Guid.NewGuid();
        private static readonly Guid Id2 = Guid.NewGuid();
        private static readonly Guid Id3 = Guid.NewGuid();

        private static LibraryVersionIdentity[] Project()
        {
            return new[]
            {
                new LibraryVersionIdentity(V1, Id1, true),
                new LibraryVersionIdentity(V2, Id2, true),
                new LibraryVersionIdentity(V3, Id3, false)
            };
        }

        [TestMethod]
        public void RequireNewVersions_TheGlobalLibraryBehind_ReturnsTheReleasedOnesItLacks()
        {
            var global = new[] { new LibraryVersionIdentity(V1, Id1, true) };

            var added = GlobalLibraryUpdateRules.RequireNewVersions("FC_Motor", Project(), global);

            CollectionAssert.AreEqual(new[] { V2 }, added.ToArray());
        }

        [TestMethod]
        public void RequireNewVersions_TypeNotInTheGlobalLibrary_IsRefusedPointingToPublish()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => GlobalLibraryUpdateRules.RequireNewVersions("FC_Motor", Project(), null));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "PublishLibraryTypes", StringComparison.Ordinal);
        }

        /// <remarks>The same number with another GUID is another version: the global library went its own way.</remarks>
        [TestMethod]
        public void RequireNewVersions_AGlobalVersionTheProjectNeverHad_IsRefused()
        {
            var global = new[] { new LibraryVersionIdentity(V1, Id1, true), new LibraryVersionIdentity(V2, Guid.NewGuid(), true) };

            var refusal = Assert.ThrowsException<PortalException>(() => GlobalLibraryUpdateRules.RequireNewVersions("FC_Motor", Project(), global));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "0.0.2", StringComparison.Ordinal);
        }

        /// <remarks>A version in work is not taken by TIA Portal (measured 2026-11-24), so it is not counted as new.</remarks>
        [TestMethod]
        public void RequireNewVersions_OnlyAVersionInWorkIsNew_IsRefusedPointingToRelease()
        {
            var global = new[] { new LibraryVersionIdentity(V1, Id1, true), new LibraryVersionIdentity(V2, Id2, true) };

            var refusal = Assert.ThrowsException<PortalException>(() => GlobalLibraryUpdateRules.RequireNewVersions("FC_Motor", Project(), global));

            StringAssert.Contains(refusal.Message, "ReleaseLibraryType", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireDependenciesPresent_ADependencyTheGlobalLibraryHolds_Passes()
        {
            var dependency = Guid.NewGuid();

            GlobalLibraryUpdateRules.RequireDependenciesPresent("FB_Motor", new[] { ("UDT_Motor", V1, dependency) }, new[] { dependency });
        }

        [TestMethod]
        public void RequireDependenciesPresent_ADependencyMissing_IsRefusedNamingIt()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => GlobalLibraryUpdateRules.RequireDependenciesPresent("FB_Motor", new[] { ("UDT_Motor", V2, Guid.NewGuid()) }, new[] { Id1 }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "UDT_Motor 0.0.2", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireArrived_EveryVersionThere_Passes()
        {
            GlobalLibraryUpdateRules.RequireArrived("FC_Motor", new[] { V2 }, Project());
        }

        [TestMethod]
        public void RequireArrived_AVersionMissing_FailsAsAWriteFailure()
        {
            var after = new[] { new LibraryVersionIdentity(V1, Id1, true) };

            var failure = Assert.ThrowsException<PortalException>(() => GlobalLibraryUpdateRules.RequireArrived("FC_Motor", new[] { V2 }, after));

            Assert.AreEqual(PortalErrorCode.WriteFailed, failure.Code);
        }
    }
}
