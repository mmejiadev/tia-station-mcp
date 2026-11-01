using System;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class UnifiedFaceplateListReaderTests
    {
        private const string FullList = @"{ ""screen"": ""Principal"", ""faceplates"": [
            { ""name"": ""FP_M1"", ""type"": ""FP_Motor"", ""version"": ""0.0.1"", ""left"": 20, ""top"": 40, ""width"": 300,
              ""interface"": { ""Motor"": { ""tag"": ""M1"" }, ""Nom"": ""Motor 1"" } } ] }";

        [TestMethod]
        public void Parse_AFullList_ReadsTheScreenTypeVersionFrameAndInterface()
        {
            var list = UnifiedFaceplateListReader.Parse(FullList);

            var faceplate = list.Faceplates.Single();
            Assert.AreEqual("Principal", list.Screen);
            Assert.AreEqual("FP_Motor", faceplate.TypeName);
            Assert.AreEqual(new Version(0, 0, 1), faceplate.Version);
            Assert.AreEqual(300u, faceplate.Frame.Width);
            Assert.IsNull(faceplate.Frame.Height);
            Assert.IsTrue(faceplate.Interface["Motor"].IsTag);
            Assert.AreEqual("M1", faceplate.Interface["Motor"].Value);
            Assert.IsFalse(faceplate.Interface["Nom"].IsTag);
        }

        [TestMethod]
        public void Parse_NoVersion_LeavesItForTheNewestReleased()
        {
            var list = UnifiedFaceplateListReader.Parse(@"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""FP_Motor"", ""left"": 0, ""top"": 0 } ] }");

            Assert.IsNull(list.Faceplates.Single().Version);
        }

        [TestMethod]
        public void Parse_AVersionWithASpace_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateListReader.Parse(
                @"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""FP_Motor"", ""version"": ""V 0.0.1"", ""left"": 0, ""top"": 0 } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Parse_ANumberAsAFixedValue_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateListReader.Parse(
                @"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""FP_Motor"", ""left"": 0, ""top"": 0, ""interface"": { ""Speed"": 5 } } ] }"));

            StringAssert.Contains(refusal.Message, "{\"tag\": \"M1\"}", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_NoScreen_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateListReader.Parse(@"{ ""faceplates"": [ { ""name"": ""F"", ""type"": ""T"", ""left"": 0, ""top"": 0 } ] }"));

            StringAssert.Contains(refusal.Message, "'screen'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_AFaceplateNamedTwice_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateListReader.Parse(
                @"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""T"", ""left"": 0, ""top"": 0 }, { ""name"": ""f"", ""type"": ""T"", ""left"": 0, ""top"": 0 } ] }"));

            StringAssert.Contains(refusal.Message, "named twice", StringComparison.Ordinal);
        }

        /// <remarks>The review of 2026-10-30: the second of two keys silently replaced the first, a tag link with a text.</remarks>
        [TestMethod]
        public void Parse_AnInterfacePropertyGivenTwiceInAnyCase_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateListReader.Parse(
                @"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""T"", ""left"": 0, ""top"": 0, ""interface"": { ""Motor"": { ""tag"": ""M1"" }, ""motor"": ""x"" } } ] }"));

            StringAssert.Contains(refusal.Message, "given twice", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_AFieldNobodyReads_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedFaceplateListReader.Parse(
                @"{ ""screen"": ""S"", ""faceplates"": [ { ""name"": ""F"", ""type"": ""T"", ""left"": 0, ""top"": 0, ""rotation"": 90 } ] }"));

            StringAssert.Contains(refusal.Message, "rotation", StringComparison.Ordinal);
        }
    }
}
