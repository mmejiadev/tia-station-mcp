using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Every value refused here was taken by Openness V20 and refused only by the compile (measured
    /// 2026-10-25), so each case is one the compile would have caught too late.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTagValuesTests
    {
        [TestMethod]
        [DataRow("Int", "-32768", "-32768", DisplayName = "Int, its lowest")]
        [DataRow("USInt", "255", "255", DisplayName = "USInt, its highest")]
        [DataRow("ULInt", "18446744073709551615", "18446744073709551615", DisplayName = "ULInt, its highest")]
        [DataRow("Real", "1.5", "1.5", DisplayName = "Real with a point")]
        [DataRow("Real", "1e3", "1e3", DisplayName = "Real with an exponent")]
        [DataRow("Bool", "1", "1", DisplayName = "Bool 1")]
        [DataRow("WString", "Nivell del dipòsit", "Nivell del dipòsit", DisplayName = "WString, any text")]
        public void Constant_ValueThatFits_IsWrittenAsTiaPortalCompilesIt(string dataType, string value, string expected)
        {
            Assert.AreEqual(expected, UnifiedTagValues.Constant(dataType, value, "T"));
        }

        [TestMethod]
        [DataRow("Int", "99999", DisplayName = "Int out of range: 'outside the value range'")]
        [DataRow("Int", "1.5", DisplayName = "Int with a fraction: 'use a value with a valid type'")]
        [DataRow("Int", "abc", DisplayName = "Int as text: 'only 0123456789eE-+'")]
        [DataRow("Real", "1,5", DisplayName = "Real with a comma: 'only 0123456789eE-+'")]
        [DataRow("Real", "1e40", DisplayName = "Real beyond a float")]
        [DataRow("Bool", "true", DisplayName = "Bool as true: 'only the values 0 and 1'")]
        [DataRow("USInt", "-1", DisplayName = "unsigned below 0")]
        [DataRow("DateTime", "2026-01-01", DisplayName = "a type no value was measured for")]
        public void Constant_ValueTheCompileRefuses_ThrowsInvalidParams(string dataType, string value)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTagValues.Constant(dataType, value, "T"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Problem_ValueThatFitsAndOneThatDoesNot_AnswersWithoutThrowing()
        {
            Assert.IsNull(UnifiedTagValues.Problem("Int", "7", "T"));
            StringAssert.Contains(UnifiedTagValues.Problem("Int", "99999", "T"), "outside the range", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void RequireOrdered_LowerNotBelowUpper_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTagValues.RequireOrdered("50", "10", "T"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.ThrowsException<PortalException>(() => UnifiedTagValues.RequireOrdered("5", "5", "T"));
        }

        [TestMethod]
        [DataRow("-1.5", "1e3", DisplayName = "Real")]
        [DataRow("18446744073709551614", "18446744073709551615", DisplayName = "ULInt, one apart at the top")]
        [DataRow("9007199254740992", "9007199254740993", DisplayName = "LInt, one apart beyond a double's precision")]
        [DataRow("1e30", "1e31", DisplayName = "LReal beyond a decimal")]
        public void RequireOrdered_LowerBelowUpper_Passes(string lower, string upper)
        {
            UnifiedTagValues.RequireOrdered(lower, upper, "T");
        }

        [TestMethod]
        [DataRow("Int", true)]
        [DataRow("LReal", true)]
        [DataRow("Bool", false)]
        [DataRow("WString", false)]
        [DataRow("Array [0..3] of Int", false)]
        public void IsNumeric_TellsWhichTypesHaveLimits(string dataType, bool expected)
        {
            Assert.AreEqual(expected, UnifiedTagValues.IsNumeric(dataType));
        }
    }
}
