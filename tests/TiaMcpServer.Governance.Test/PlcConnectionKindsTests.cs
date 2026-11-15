using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The words CreatePlcConnection accepts for a connection's kind. An unknown word must be refused, never turned into
    /// the nearest kind: an S7 connection made where TCP was meant is a connection the program's instructions cannot use.
    /// </remarks>
    [TestClass]
    public sealed class PlcConnectionKindsTests
    {
        [TestMethod]
        [DataRow("S7", PlcConnectionKind.S7)]
        [DataRow("s7", PlcConnectionKind.S7)]
        [DataRow("TCP", PlcConnectionKind.Tcp)]
        [DataRow("ISO-on-TCP", PlcConnectionKind.IsoOnTcp)]
        [DataRow("iso_on_tcp", PlcConnectionKind.IsoOnTcp)]
        [DataRow(" IsoOnTcp ", PlcConnectionKind.IsoOnTcp)]
        public void Parse_AnAcceptedSpelling_ReturnsItsKind(string text, PlcConnectionKind expected)
        {
            var kind = PlcConnectionKinds.Parse(text);

            Assert.AreEqual(expected, kind);
        }

        [TestMethod]
        [DataRow("UDP")]
        [DataRow("S7-1500")]
        [DataRow("ISO")]
        public void Parse_AKindNotMade_ThrowsInvalidParamsNamingTheAcceptedOnes(string text)
        {
            var failure = Assert.ThrowsException<PortalException>(() => PlcConnectionKinds.Parse(text));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, PlcConnectionKinds.Accepted, StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void Parse_Blank_ThrowsInvalidParams(string? text)
        {
            var failure = Assert.ThrowsException<PortalException>(() => PlcConnectionKinds.Parse(text));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Name_EveryKind_NamesItAndParsesBack()
        {
            foreach (PlcConnectionKind kind in Enum.GetValues(typeof(PlcConnectionKind)))
            {
                var name = PlcConnectionKinds.Name(kind);

                Assert.AreEqual(kind, PlcConnectionKinds.Parse(name), name);
            }
        }

        /// <remarks>A value outside the enumeration must throw, never fall through to a name: see "Anything not foreseen refuses".</remarks>
        [TestMethod]
        public void Name_AnUnknownValue_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => PlcConnectionKinds.Name((PlcConnectionKind)99));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }
    }
}
