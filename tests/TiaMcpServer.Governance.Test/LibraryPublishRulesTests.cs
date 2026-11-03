using System;
using System.Collections.Generic;
using TiaMcpServer.Library;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-11-01 on TIA Portal V20: a type only in work is published as nothing at all, without a word.</remarks>
    [TestClass]
    public sealed class LibraryPublishRulesTests
    {
        private static readonly Version V001 = new Version(0, 0, 1);

        private static readonly Version V002 = new Version(0, 0, 2);

        private static readonly string[] Published = { "FP_Motor", "UDT_Motor" };

        [TestMethod]
        public void Check_ATypeOnlyInWork_IsRefusedAsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryPublishRules.Check(new[] { "FP_Motor" }, Versions().Keys, name => Versions()[name]));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "publish nothing", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ATypeTheProjectLacks_IsRefusedAsInvalidParams()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryPublishRules.Check(new[] { "FP_Valve" }, Versions().Keys, name => Versions()[name]));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "The project library has no type", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ATypeWithAReleasedVersion_PassesSilently()
        {
            LibraryPublishRules.Check(new[] { "UDT_Motor" }, Versions().Keys, name => Versions()[name]);
        }

        /// <remarks>The review of 2026-11-01: one type in a state this server does not know blocked a publish that never touched it.</remarks>
        [TestMethod]
        public void Check_ATypeNotNamed_IsNeverRead()
        {
            var read = new List<string>();

            LibraryPublishRules.Check(new[] { "UDT_Motor" }, Versions().Keys, name =>
            {
                read.Add(name);
                return Versions()[name];
            });

            CollectionAssert.AreEqual(new[] { "UDT_Motor" }, read);
        }

        [TestMethod]
        public void CheckPublished_ATypeThatDidNotArrive_IsRefusedAsWriteFailed()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => LibraryPublishRules.CheckPublished(Published, new[] { "fp_motor" }));

            Assert.AreEqual(PortalErrorCode.WriteFailed, refusal.Code);
            StringAssert.Contains(refusal.Message, "'UDT_Motor'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void CheckPublished_EveryTypeArrived_PassesSilently()
        {
            LibraryPublishRules.CheckPublished(Published, new[] { "UDT_MOTOR", "fp_motor", "Other" });
        }

        [TestMethod]
        public void ReleasedVersion_NoneNamed_IsTheNewestReleased()
        {
            var versions = new[] { new LibraryVersionInfo(V002, isReleased: true), new LibraryVersionInfo(V001, isReleased: true), new LibraryVersionInfo(new Version(0, 0, 3), isReleased: false) };

            Assert.AreEqual(V002, LibraryPublishRules.ReleasedVersion("FP_Motor", versions, null));
        }

        [TestMethod]
        public void ReleasedVersion_AVersionInWorkNamed_IsRefusedNamingTheReleasedOnes()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: true), new LibraryVersionInfo(V002, isReleased: false) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryPublishRules.ReleasedVersion("FP_Motor", versions, V002));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "released: 0.0.1", StringComparison.Ordinal);
        }

        [TestMethod]
        public void ReleasedVersion_NothingReleased_IsRefusedAsInvalidState()
        {
            var versions = new[] { new LibraryVersionInfo(V001, isReleased: false) };

            var refusal = Assert.ThrowsException<PortalException>(() => LibraryPublishRules.ReleasedVersion("FP_Motor", versions, null));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void GlobalLibrary_APolicyForTheProjectLibrary_DoesNotReachIt()
        {
            var projectRule = TargetPattern.Parse("library/*");

            Assert.AreEqual("global-library/C:/Libraries/Kit", ChangeTarget.GlobalLibraryFolder(@"C:\Libraries\Kit"));
            Assert.IsFalse(projectRule.Matches(ChangeTarget.GlobalLibraryFolder(@"C:\Libraries\Kit")));
        }

        private static Dictionary<string, IReadOnlyList<LibraryVersionInfo>> Versions()
        {
            return new Dictionary<string, IReadOnlyList<LibraryVersionInfo>>(StringComparer.OrdinalIgnoreCase)
            {
                ["FP_Motor"] = new[] { new LibraryVersionInfo(V001, isReleased: false) },
                ["UDT_Motor"] = new[] { new LibraryVersionInfo(V001, isReleased: true) }
            };
        }
    }
}
