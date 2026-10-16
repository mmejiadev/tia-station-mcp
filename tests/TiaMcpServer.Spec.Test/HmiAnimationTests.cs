using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// Animations on any item, in the shapes the class exports have. Whether TIA Portal accepts each
    /// kind on each item is for <c>Test40Hmi</c> to say; these say the text came out as described.
    /// </remarks>
    [TestClass]
    public sealed class HmiAnimationTests
    {
        private static XDocument Build(string item)
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [ " + item + " ] }");

            return XDocument.Parse(HmiScreenDocument.Build(screen));
        }

        private static PortalException Refusal(string item)
        {
            return Assert.ThrowsException<PortalException>(
                () => HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [ " + item + " ] }"));
        }

        [TestMethod]
        public void Build_ThreeRangesOnAnIoField_WritesEachRangeWithItsLimits()
        {
            var screen = Build(@"{ ""type"": ""ioField"", ""left"": 0, ""top"": 0, ""tag"": ""Level"", ""animations"": [
                { ""type"": ""appearance"", ""tag"": ""Level"", ""ranges"": [
                    { ""from"": 0, ""to"": 49, ""backColor"": ""green"" },
                    { ""from"": 50, ""to"": 79, ""backColor"": ""yellow"" },
                    { ""from"": 80, ""to"": 100, ""backColor"": ""red"", ""flashing"": true } ] } ] }");

            var limits = screen.Descendants("Hmi.Dynamic.Range")
                .Select(range => range.Element("AttributeList")!.Element("LowerLimit")!.Value + "-" + range.Element("AttributeList")!.Element("UpperLimit")!.Value)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "0-49", "50-79", "80-100" }, limits);
        }

        [TestMethod]
        public void Build_FlashingRange_WritesMediumFlashingOnlyThere()
        {
            var screen = Build(@"{ ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""ALARM"", ""animations"": [
                { ""type"": ""appearance"", ""tag"": ""Alarm"", ""ranges"": [ { ""from"": 0, ""backColor"": ""grey"" }, { ""from"": 1, ""backColor"": ""red"", ""flashing"": true } ] } ] }");

            var flashing = screen.Descendants("FlashingType").Select(type => type.Value).ToArray();

            CollectionAssert.AreEqual(new[] { "No", "Medium" }, flashing);
        }

        /// <remarks>A range that names no background keeps the item's own: the export always writes one.</remarks>
        [TestMethod]
        public void Build_RangeWithoutBackground_KeepsTheItemsBackground()
        {
            var screen = Build(@"{ ""type"": ""ioField"", ""left"": 0, ""top"": 0, ""tag"": ""T"", ""backColor"": ""black"", ""animations"": [
                { ""type"": ""appearance"", ""tag"": ""T"", ""ranges"": [ { ""from"": 1, ""foreColor"": ""red"" } ] } ] }");

            Assert.AreEqual("0, 0, 0", screen.Descendants("Hmi.Dynamic.Range").Single().Element("AttributeList")!.Element("BackColor")!.Value);
        }

        [TestMethod]
        public void Build_VisibilityOnAButton_HidesItOnTheGivenBit()
        {
            var screen = Build(@"{ ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Jog"", ""press"": ""setBitWhilePressed"", ""tag"": ""Jog"", ""animations"": [
                { ""type"": ""visibility"", ""tag"": ""Mode"", ""bit"": 3, ""visible"": false } ] }");

            var visibility = screen.Descendants("Hmi.Dynamic.SingleBitVisibilityAnimation").Single().Element("AttributeList")!;
            Assert.AreEqual("3", visibility.Element("BitPosition")!.Value);
            Assert.AreEqual("false", visibility.Element("Visible")!.Value);
        }

        [TestMethod]
        public void Parse_AppearanceWithoutRanges_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""animations"": [ { ""type"": ""appearance"", ""tag"": ""T"" } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_RangeEndingBelowItsStart_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""animations"": [
                { ""type"": ""appearance"", ""tag"": ""T"", ""ranges"": [ { ""from"": 5, ""to"": 1 } ] } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>Every range belongs in one appearance animation; two of a kind on one object are refused.</remarks>
        [TestMethod]
        public void Parse_TwoAppearanceAnimationsOnOneItem_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""animations"": [
                { ""type"": ""appearance"", ""tag"": ""T"", ""ranges"": [ { ""from"": 0 } ] },
                { ""type"": ""appearance"", ""tag"": ""U"", ""ranges"": [ { ""from"": 1 } ] } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>A pilot light carries its own appearance animation; a second one would be two of a kind.</remarks>
        [TestMethod]
        public void Parse_PilotLightWithAnAppearanceAnimation_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""pilotLight"", ""left"": 0, ""top"": 0, ""tag"": ""KM1"", ""animations"": [
                { ""type"": ""appearance"", ""tag"": ""T"", ""ranges"": [ { ""from"": 0 } ] } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_PilotLightWithVisibleTagAndAVisibilityAnimation_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""pilotLight"", ""left"": 0, ""top"": 0, ""tag"": ""KM1"", ""visibleTag"": ""Alarm"", ""animations"": [
                { ""type"": ""visibility"", ""tag"": ""X"" } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_UnknownAnimationType_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""animations"": [ { ""type"": ""spin"", ""tag"": ""T"" } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_VisibilityBitOutOfRange_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""animations"": [ { ""type"": ""visibility"", ""tag"": ""T"", ""bit"": 32 } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
