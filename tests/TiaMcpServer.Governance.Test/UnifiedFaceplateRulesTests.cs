using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: an interface property takes a tag that does not exist, and a
    /// version in work cannot be placed; only the compile, or Openness's own refusal, would say so.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedFaceplateRulesTests
    {
        private static readonly Version V001 = new Version(0, 0, 1);

        private static readonly Version V002 = new Version(0, 0, 2);

        [TestMethod]
        public void Check_ATagThatDoesNotExist_IsRefusedNamingIt()
        {
            var rules = Rules(tags: new[] { "M1" });

            var refusal = Assert.ThrowsException<PortalException>(() => rules.Check(List(@"""interface"": { ""Motor"": { ""tag"": ""M9"" } }")));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "'M9', which does not exist", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_AMemberOfAnExistingTag_IsTaken()
        {
            Rules(tags: new[] { "M1" }).Check(List(@"""interface"": { ""Fault"": { ""tag"": ""M1.Ft"" } }"));
        }

        [TestMethod]
        public void Check_AFixedTextThatIsNoTagName_IsNotCheckedAsATag()
        {
            Rules(tags: Array.Empty<string>()).Check(List(@"""interface"": { ""Nom"": ""Motor 1"" }"));
        }

        [TestMethod]
        public void Check_ATypeWithOnlyAVersionInWork_IsRefused()
        {
            var rules = Rules(released: new Dictionary<string, IReadOnlyList<Version>> { ["FP_Motor"] = Array.Empty<Version>() });

            var refusal = Assert.ThrowsException<PortalException>(() => rules.Check(List(string.Empty)));

            StringAssert.Contains(refusal.Message, "ReleaseLibraryType", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ATypeTheProjectLibraryLacks_IsRefused()
        {
            var rules = Rules(released: new Dictionary<string, IReadOnlyList<Version>>());

            var refusal = Assert.ThrowsException<PortalException>(() => rules.Check(List(string.Empty)));

            StringAssert.Contains(refusal.Message, "CopyLibraryTypes", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_AVersionNotReleased_IsRefusedNamingTheReleasedOnes()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => Rules().Check(List(@"""version"": ""0.0.9""")));

            StringAssert.Contains(refusal.Message, "released: 0.0.1, 0.0.2", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ANameTakenOnTheScreen_IsRefusedAsInvalidState()
        {
            var rules = Rules(screenItems: new[] { "fp_m1" });

            var refusal = Assert.ThrowsException<PortalException>(() => rules.Check(List(string.Empty)));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }

        [TestMethod]
        public void Check_ATagInAnotherCase_IsFound()
        {
            Rules(tags: new[] { "M1" }).Check(List(@"""interface"": { ""Motor"": { ""tag"": ""m1"" } }"));
        }

        [TestMethod]
        public void ValueOf_ATagInAnotherCase_IsWrittenAsTiaPortalSpellsItWithItsMember()
        {
            var faceplate = List(@"""interface"": { ""Fault"": { ""tag"": ""m1.Ft"" } }").Faceplates.Single();

            Assert.AreEqual("M1.Ft", Rules(tags: new[] { "M1" }).ValueOf(faceplate.Interface["Fault"]));
        }

        [TestMethod]
        public void TypeNameOf_ATypeInAnotherCase_IsTheNameTiaPortalHolds()
        {
            var faceplate = UnifiedFaceplateListReader.Parse(@"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""fp_motor"", ""left"": 0, ""top"": 0 } ] }").Faceplates.Single();

            Assert.AreEqual("FP_Motor", Rules().TypeNameOf(faceplate));
        }

        /// <remarks>The review of 2026-10-30: a version given explicitly was never compared against one read in four parts.</remarks>
        [TestMethod]
        public void Check_AVersionGivenInThreePartsAndReleasedInFour_IsFound()
        {
            var released = new Dictionary<string, IReadOnlyList<Version>> { ["FP_Motor"] = new[] { new Version(0, 0, 1, 0) } };

            Rules(released: released).Check(List(@"""version"": ""0.0.1"""));
        }

        [TestMethod]
        public void VersionOf_NoVersionGiven_IsTheNewestReleased()
        {
            var faceplate = List(string.Empty).Faceplates.Single();

            Assert.AreEqual(V002, Rules().VersionOf(faceplate));
        }

        private static UnifiedFaceplateRules Rules(IReadOnlyCollection<string>? screenItems = null, IReadOnlyDictionary<string, IReadOnlyList<Version>>? released = null, IReadOnlyCollection<string>? tags = null)
        {
            return new UnifiedFaceplateRules(new UnifiedFaceplateState(screenItems ?? new[] { "Frame" }, released ?? Released(), tags ?? new[] { "M1" }));
        }

        private static Dictionary<string, IReadOnlyList<Version>> Released()
        {
            return new Dictionary<string, IReadOnlyList<Version>> { ["FP_Motor"] = new[] { V002, V001 } };
        }

        private static UnifiedFaceplateList List(string extraFields)
        {
            var extra = extraFields.Length == 0 ? string.Empty : ", " + extraFields;

            return UnifiedFaceplateListReader.Parse($@"{{ ""screen"": ""Principal"", ""faceplates"": [ {{ ""name"": ""FP_M1"", ""type"": ""FP_Motor"", ""left"": 0, ""top"": 0{extra} }} ] }}");
        }
    }
}
