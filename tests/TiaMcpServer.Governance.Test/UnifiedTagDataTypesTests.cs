using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Arrays as TIA Portal V20 takes them for an internal tag (measured 2026-10-25): one dimension, from 0.</remarks>
    [TestClass]
    public sealed class UnifiedTagDataTypesTests
    {
        [TestMethod]
        [DataRow("Array[0..3] of int", "Array [0..3] of Int", DisplayName = "unspaced, element in another case")]
        [DataRow("array [ 0 .. 0 ] of WString", "Array [0..0] of WString", DisplayName = "one element, spaced")]
        [DataRow("bool", "Bool", DisplayName = "a single value")]
        public void NormaliseInternal_MeasuredShapes_AreSpelledAsTiaPortalReadsThemBack(string given, string expected)
        {
            Assert.AreEqual(expected, UnifiedTagDataTypes.NormaliseInternal(given, "T"));
        }

        [TestMethod]
        [DataRow("Array[1..10] of Bool", DisplayName = "a lower bound other than 0")]
        [DataRow("Array[0..1, 0..1] of Int", DisplayName = "two dimensions")]
        [DataRow("Array[0..-1] of Int", DisplayName = "an end below the start")]
        [DataRow("Array[0..3] of String", DisplayName = "an element type TIA Portal refused")]
        [DataRow("Array[0..3] of Array[0..1] of Int", DisplayName = "an array of arrays")]
        [DataRow("Struct", DisplayName = "a structure")]
        [DataRow("Array[0..99999999999999999999] of Int", DisplayName = "a bound too long for a number")]
        [DataRow("Array[0..\u0663] of Int", DisplayName = "a digit that is not ASCII")]
        public void NormaliseInternal_ShapeTiaPortalRefuses_ThrowsInvalidParams(string given)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedTagDataTypes.NormaliseInternal(given, "T"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void IsArray_ArrayAndSingleValue_TellsThemApart()
        {
            Assert.IsTrue(UnifiedTagDataTypes.IsArray("Array [0..3] of Int"));
            Assert.IsFalse(UnifiedTagDataTypes.IsArray("Int"));
        }
    }
}
