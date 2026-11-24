using System;
using TiaMcpServer.Library;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// TIA Portal V21 starts a new version of a block or data type type only through an instance of it (measured 2026-11-24), so
    /// which instance, and what to do without one, is decided here.
    /// </remarks>
    [TestClass]
    public sealed class LibraryInstanceRulesTests
    {
        private static readonly Version V1 = new Version(0, 0, 1);
        private static readonly Version V2 = new Version(0, 0, 2);

        [TestMethod]
        public void RequireInstanceOn_AnInstanceOnTheNewest_Passes()
        {
            LibraryInstanceRules.RequireInstanceOn("FC_Motor", "PLC_1", V2, new[] { new Version(0, 0, 2, 0) });
        }

        [TestMethod]
        public void RequireInstanceOn_NoInstance_IsRefusedPointingToCreatingOne()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryInstanceRules.RequireInstanceOn("FC_Motor", "PLC_1", V2, Array.Empty<Version>()));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "CreatePlcBlockFromLibrary", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireInstanceOn_AnInstanceOnAnOlderVersion_IsRefusedPointingToTheUpdate()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryInstanceRules.RequireInstanceOn("FC_Motor", "PLC_1", V2, new[] { V1 }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "UpdatePlcLibraryInstances", StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireNoneInWork_EveryInstanceOnAReleasedVersion_Passes()
        {
            LibraryInstanceRules.RequireNoneInWork("FC_Motor", "PLC_1", new[] { V1, V2 }, new[] { new Version(0, 0, 1, 0) });
        }

        /// <remarks>Moving the test instance of a version in work would delete what that version is edited through.</remarks>
        [TestMethod]
        public void RequireNoneInWork_AnInstanceOnAVersionInWork_IsRefusedNamingIt()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryInstanceRules.RequireNoneInWork("FC_Motor", "PLC_1", new[] { V1 }, new[] { V2 }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "0.0.2", StringComparison.Ordinal);
        }
    }
}
