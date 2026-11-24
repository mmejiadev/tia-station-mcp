using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Measured 2026-11-19 on V21: TIA Portal takes any text as a tag's address, so this is the only check there is.</remarks>
    [TestClass]
    public sealed class PlcTagAddressTests
    {
        [TestMethod]
        [DataRow("%I0.0", "%I0.0")]
        [DataRow("%q7.7", "%Q7.7")]
        [DataRow(" %mw10 ", "%MW10")]
        [DataRow("%QB1", "%QB1")]
        [DataRow("%ID4", "%ID4")]
        [DataRow("%ML8", "%ML8")]
        [DataRow("%I65535.7", "%I65535.7")]
        public void Parse_AnInputOutputOrMemoryAddress_IsTakenInUpperCase(string written, string expected)
        {
            var address = PlcTagAddress.Parse(written, "Start");

            Assert.AreEqual(expected, address);
        }

        [TestMethod]
        [DataRow("nonsense")]
        [DataRow("%Q0.8")]
        [DataRow("%M0.0.0")]
        [DataRow("%DB1.DBX0.0")]
        [DataRow("%Q9999999.9")]
        [DataRow("%I0")]
        [DataRow("I0.0")]
        [DataRow("%I٣.0")]
        [DataRow("")]
        [DataRow(null)]
        public void Parse_NotATagAddress_IsRefusedAsInvalidParams(string? written)
        {
            var failure = Assert.ThrowsException<PortalException>(() => PlcTagAddress.Parse(written, "Start"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "'Start' is not a tag address", StringComparison.Ordinal);
        }
    }
}
