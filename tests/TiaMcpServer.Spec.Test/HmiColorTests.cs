using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    [TestClass]
    public sealed class HmiColorTests
    {
        [TestMethod]
        public void Parse_Name_GivesTiaPortalsSpelling()
        {
            Assert.AreEqual("0, 255, 0", HmiColor.Parse("Green", "test"));
        }

        [TestMethod]
        public void Parse_Hex_GivesTiaPortalsSpelling()
        {
            Assert.AreEqual("255, 0, 255", HmiColor.Parse("#FF00FF", "test"));
        }

        [TestMethod]
        public void Parse_TripleWithoutSpaces_IsNormalised()
        {
            Assert.AreEqual("24, 28, 49", HmiColor.Parse("24,28,49", "test"));
        }

        [TestMethod]
        public void Parse_ChannelOutOfRange_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiColor.Parse("256, 0, 0", "test"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
