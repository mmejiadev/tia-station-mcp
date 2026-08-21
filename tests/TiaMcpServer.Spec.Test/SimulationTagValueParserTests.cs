using System.Globalization;
using System.Threading;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Test
{
    /// <summary>
    /// The text of a tag write becomes the value the caller meant, or it is refused.
    /// </summary>
    /// <remarks>
    /// This class exists because of a defect it would have caught in three lines. The parser was
    /// written with <c>NumberStyles.Any</c>, which includes <c>AllowThousands</c>, so under the
    /// invariant culture <c>'1,5'</c> parsed to **15** — and the comment above it claimed that
    /// rejecting exactly that was the point of the method. Nothing could reach the type to test it,
    /// because it is internal and no test project was let in. Both are fixed.
    ///
    /// A wrong number here is not a wrong number in a report. It is written to a controller, and
    /// the read-back reports it as a success.
    /// </remarks>
    [TestClass]
    public sealed class SimulationTagValueParserTests
    {
        [TestMethod]
        public void ToReal_ACommaAsTheDecimalSeparator_IsRefusedRatherThanReadAsThousands()
        {
            // The regression. '1,5' is what a Spanish keyboard produces for one and a half, and the
            // one thing that must not happen is for it to arrive at a controller as fifteen.
            var exception = Assert.ThrowsException<PortalException>(() => SimulationTagValueParser.ToReal("1,5"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, "decimal point", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void ToDInt_AGroupSeparator_IsRefused()
        {
            // '1,000' meaning a thousand is the other half of the same defect, and the error message
            // promises this is refused.
            Assert.ThrowsException<PortalException>(() => SimulationTagValueParser.ToDInt("1,000"));
        }

        [TestMethod]
        public void ToDInt_AccountingParentheses_IsRefused()
        {
            // NumberStyles.Any accepted '(5)' as -5. Nobody writing a PLC tag means that.
            Assert.ThrowsException<PortalException>(() => SimulationTagValueParser.ToDInt("(5)"));
        }

        [TestMethod]
        public void ToReal_ADecimalPoint_MeansTheSameOnASpanishMachine()
        {
            // The claim the class makes: the same tool call means the same value whatever the
            // machine's locale is. Asserted by changing the locale rather than by trusting it.
            var original = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

                Assert.AreEqual(1.5f, SimulationTagValueParser.ToReal("1.5"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void ToDInt_ANegativeNumberWithSurroundingSpace_Parses()
        {
            // Narrowing the styles must not have narrowed them past what a caller legitimately
            // sends. A sign and surrounding whitespace are legitimate; a group separator is not.
            Assert.AreEqual(-42, SimulationTagValueParser.ToDInt("  -42 "));
        }

        [TestMethod]
        public void ToInt_ANumberTooBigForTheType_IsRefused()
        {
            // 32768 is one past an Int, and the PLC type is what decides. Reported as bad input
            // rather than written as something else.
            Assert.ThrowsException<PortalException>(() => SimulationTagValueParser.ToInt("32768"));
        }

        [TestMethod]
        public void ToBool_TheFourSpellingsACallerActuallyUses_AllParse()
        {
            // TRUE and FALSE are how SCL spells them; 1 and 0 are how a watch table shows them.
            Assert.IsTrue(SimulationTagValueParser.ToBool("true"));
            Assert.IsTrue(SimulationTagValueParser.ToBool("TRUE"));
            Assert.IsTrue(SimulationTagValueParser.ToBool("1"));
            Assert.IsFalse(SimulationTagValueParser.ToBool("0"));
        }

        [TestMethod]
        public void ToBool_SomethingThatIsNotABool_IsRefusedWithBothSpellingsOffered()
        {
            var exception = Assert.ThrowsException<PortalException>(() => SimulationTagValueParser.ToBool("yes"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, "'true'", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void ToWChar_MoreThanOneCharacter_IsRefused()
        {
            Assert.AreEqual('A', SimulationTagValueParser.ToWChar("A"));

            Assert.ThrowsException<PortalException>(() => SimulationTagValueParser.ToWChar("AB"));
        }
    }
}
