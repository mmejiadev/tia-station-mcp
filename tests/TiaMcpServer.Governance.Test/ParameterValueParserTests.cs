using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The conversion between what an MCP caller can send — text — and what Openness takes — an
    /// object of the right type. It needs no TIA Portal, which is the point: this is where a bad
    /// value is caught, and catching it here is what turns an unhelpful Openness failure into a
    /// message naming the type, or the words an enumeration accepts.
    /// </remarks>
    [TestClass]
    public sealed class ParameterValueParserTests
    {
        private enum Protection
        {
            None,
            WriteProtection,
            FullProtection
        }

        [TestMethod]
        public void Parse_ANumberWhereANumberIs_ReturnsTheSameType()
        {
            var parsed = ParameterValueParser.Parse(100, "150", "CycleTime");

            Assert.AreEqual(150, parsed);
            Assert.IsInstanceOfType(parsed, typeof(int));
        }

        [TestMethod]
        public void Parse_TrueWhereABooleanIs_ReturnsABoolean()
        {
            var parsed = ParameterValueParser.Parse(false, "true", "AutoStart");

            Assert.AreEqual(true, parsed);
        }

        /// <remarks>
        /// The enumeration case is the one that earns this class its place. A protection level is
        /// a word whose spelling appears in no documentation the caller has read, so the refusal
        /// carries the list.
        /// </remarks>
        [TestMethod]
        public void Parse_AWordAnEnumerationTakes_ReturnsTheEnumerationValue()
        {
            var parsed = ParameterValueParser.Parse(Protection.None, "fullprotection", "ProtectionLevel");

            Assert.AreEqual(Protection.FullProtection, parsed);
        }

        [TestMethod]
        public void Parse_AWordAnEnumerationDoesNotTake_ListsTheOnesItDoes()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ParameterValueParser.Parse(Protection.None, "Locked", "ProtectionLevel"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "WriteProtection", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_TextWhereANumberIs_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ParameterValueParser.Parse(100, "soon", "CycleTime"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_SomethingOtherThanTrueOrFalse_SaysWhichTwoWordsItTakes()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ParameterValueParser.Parse(false, "yes", "AutoStart"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "'true' or 'false'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_TextWhereTextIs_PassesItThroughUntouched()
        {
            var parsed = ParameterValueParser.Parse("PLC_1", "Cell controller", "Comment");

            Assert.AreEqual("Cell controller", parsed);
        }

        /// <remarks>
        /// A decimal written as 1,5 on one machine and 1.5 on another would be two different
        /// numbers. The caller is a program, so the invariant reading is the only defensible one.
        /// </remarks>
        [TestMethod]
        public void Parse_ADecimal_ReadsItTheSameWhateverTheMachineIsSetTo()
        {
            var parsed = ParameterValueParser.Parse(1.0d, "1.5", "Threshold");

            Assert.AreEqual(1.5d, parsed);
        }

        /// <remarks>
        /// The mistake the invariant reading exists to reject, and the one it first accepted:
        /// <c>Convert.ChangeType</c> allows a thousands separator, so '0,5' came back as 5 — the
        /// half a Spanish machine prints, written back ten times larger.
        /// </remarks>
        [TestMethod]
        public void Parse_ADecimalWrittenWithAComma_IsRefusedRatherThanReadAsThousands()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ParameterValueParser.Parse(1.0d, "0,5", "Threshold"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// Without a current value there is no type to convert to, and guessing would write a
        /// string into something that is not one. It reports rather than guesses.
        /// </remarks>
        [TestMethod]
        public void Parse_NothingThere_ThrowsInvalidStateRatherThanGuessing()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ParameterValueParser.Parse(null, "5", "Unset"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void Parse_ATypeItCannotWrite_SaysSoByName()
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => ParameterValueParser.Parse(DateTime.Now, "tomorrow", "LastModified"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "DateTime", StringComparison.Ordinal);
        }
    }
}
