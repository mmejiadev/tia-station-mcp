using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// What CreateTag and CreateConstant read before TIA Portal is touched. A tag's address is checked because TIA Portal takes
    /// any text as one (measured 2026-11-19); a constant's value shares the field and is not an address.
    /// </remarks>
    [TestClass]
    public sealed class TagDefinitionTests
    {
        [TestMethod]
        public void ForTag_ALowerCaseAddress_IsKeptInUpperCase()
        {
            var definition = TagDefinition.ForTag("Default tag table/Start", "Bool", "%i0.1");

            Assert.AreEqual("%I0.1", definition.Assignment);
            Assert.AreEqual("Default tag table", definition.TablePath);
            Assert.AreEqual("Start", definition.Name);
        }

        [TestMethod]
        [DataRow("nonsense")]
        [DataRow("%Q0.8")]
        [DataRow("%DB1.DBX0.0")]
        [DataRow("500")]
        public void ForTag_NotATagAddress_IsRefusedAsInvalidParams(string address)
        {
            var failure = Assert.ThrowsException<PortalException>(() => TagDefinition.ForTag("Default tag table/Start", "Bool", address));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "is not a tag address", StringComparison.Ordinal);
        }

        /// <remarks>A missing address is told as missing, not as an address that is wrong.</remarks>
        [TestMethod]
        public void ForTag_NoAddress_SaysOneIsRequired()
        {
            var failure = Assert.ThrowsException<PortalException>(() => TagDefinition.ForTag("Default tag table/Start", "Bool", " "));

            StringAssert.Contains(failure.Message, "is required", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Constructor_AConstantsValue_IsNotJudgedAsAnAddress()
        {
            var definition = new TagDefinition("Default tag table/CYCLE_MS", "Int", "500");

            Assert.AreEqual("500", definition.Assignment);
        }
    }
}
