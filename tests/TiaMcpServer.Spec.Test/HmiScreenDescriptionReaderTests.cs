using System;
using System.Linq;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// A description that is incomplete must be refused before anything reaches TIA Portal, and the
    /// refusal must say which item and what is missing: these descriptions are written by an agent
    /// that will act on the message.
    /// </remarks>
    [TestClass]
    public sealed class HmiScreenDescriptionReaderTests
    {
        private static PortalException Refusal(string json)
        {
            return Assert.ThrowsException<PortalException>(() => HmiScreenDescriptionReader.Parse(json));
        }

        private static string OneItem(string item)
        {
            return @"{ ""name"": ""S"", ""number"": 9, ""items"": [ " + item + " ] }";
        }

        [TestMethod]
        public void Parse_UnknownItemType_ThrowsInvalidParamsListingTheKnownOnes()
        {
            var failure = Refusal(OneItem(@"{ ""type"": ""slider"", ""left"": 0, ""top"": 0 }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "pilotLight", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ButtonWithoutPress_ThrowsInvalidParamsNamingTheItem()
        {
            var failure = Refusal(OneItem(@"{ ""type"": ""button"", ""name"": ""Start"", ""left"": 0, ""top"": 0, ""text"": ""Start"" }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "'Start'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ButtonsOnTwoLayers_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""press"": ""setBitWhilePressed"", ""tag"": ""A"", ""layer"": 3 },
                { ""type"": ""button"", ""left"": 0, ""top"": 40, ""text"": ""B"", ""press"": ""setBitWhilePressed"", ""tag"": ""B"" } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "one layer", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ItemWithoutLeft_ThrowsInvalidParams()
        {
            var failure = Refusal(OneItem(@"{ ""type"": ""text"", ""top"": 0, ""text"": ""Title"" }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_SetBitButtonWithoutTag_ThrowsInvalidParams()
        {
            var failure = Refusal(OneItem(@"{ ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Go"", ""press"": ""setBitWhilePressed"" }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        [DataRow(@"{ ""type"": ""line"", ""left"": 0, ""top"": 0, ""endLeft"": 10 }", DisplayName = "line without endTop")]
        [DataRow(@"{ ""type"": ""line"", ""left"": 0, ""top"": 0, ""endLeft"": 10, ""endTop"": 5, ""lineWidth"": 0 }", DisplayName = "line of width 0")]
        [DataRow(@"{ ""type"": ""graphicView"", ""left"": 0, ""top"": 0 }", DisplayName = "graphic view without picture")]
        [DataRow(@"{ ""type"": ""rectangle"", ""left"": 0, ""top"": 0, ""layer"": 32 }", DisplayName = "layer above 31")]
        [DataRow(@"{ ""type"": ""group"", ""items"": [ { ""type"": ""rectangle"", ""left"": 0, ""top"": 0 } ] }", DisplayName = "group of one")]
        [DataRow(@"{ ""type"": ""group"", ""items"": [ { ""type"": ""rectangle"", ""left"": 0, ""top"": 0 }, { ""type"": ""button"", ""left"": 0, ""top"": 40, ""text"": ""Go"", ""press"": ""setBitWhilePressed"", ""tag"": ""Go"" } ] }", DisplayName = "group with a button")]
        [DataRow(@"{ ""type"": ""group"", ""items"": [ { ""type"": ""rectangle"", ""left"": 0, ""top"": 0, ""layer"": 2 }, { ""type"": ""circle"", ""left"": 0, ""top"": 40 } ] }", DisplayName = "layer inside a group")]
        [DataRow(@"{ ""type"": ""group"", ""animations"": [ { ""type"": ""visibility"", ""tag"": ""T"" } ], ""items"": [ { ""type"": ""rectangle"", ""left"": 0, ""top"": 0 }, { ""type"": ""circle"", ""left"": 0, ""top"": 40 } ] }", DisplayName = "animated group")]
        [DataRow(@"{ ""type"": ""group"", ""left"": 300, ""items"": [ { ""type"": ""rectangle"", ""left"": 0, ""top"": 0 }, { ""type"": ""circle"", ""left"": 0, ""top"": 40 } ] }", DisplayName = "group given a position")]
        [DataRow(@"{ ""type"": ""group"", ""backColor"": ""blue"", ""items"": [ { ""type"": ""rectangle"", ""left"": 0, ""top"": 0 }, { ""type"": ""circle"", ""left"": 0, ""top"": 40 } ] }", DisplayName = "group given a colour")]
        [DataRow(@"{ ""type"": ""rectangle"", ""left"": 0, ""top"": 0, ""align"": ""Center"" }", DisplayName = "rectangle given an alignment")]
        [DataRow(@"{ ""type"": ""circle"", ""left"": 0, ""top"": 0, ""foreColor"": ""red"" }", DisplayName = "circle given a foreground colour")]
        [DataRow(@"{ ""type"": ""graphicView"", ""left"": 0, ""top"": 0, ""picture"": ""Logo"", ""fontSize"": 12 }", DisplayName = "graphic view given a font")]
        [DataRow(@"{ ""type"": ""line"", ""left"": 0, ""top"": 0, ""endLeft"": 50, ""endTop"": 0, ""width"": 50 }", DisplayName = "line given a width")]
        [DataRow(@"{ ""type"": ""line"", ""left"": 10, ""top"": 10, ""endLeft"": 10, ""endTop"": 10 }", DisplayName = "line of no length")]
        public void Parse_IncompleteShapeOrGroup_ThrowsInvalidParams(string item)
        {
            var failure = Refusal(OneItem(item));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_NameRepeatedInsideAGroup_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""name"": ""S"", ""number"": 9, ""items"": [
                { ""type"": ""text"", ""name"": ""Same"", ""left"": 0, ""top"": 0, ""text"": ""A"" },
                { ""type"": ""group"", ""items"": [ { ""type"": ""rectangle"", ""name"": ""Same"", ""left"": 0, ""top"": 0 }, { ""type"": ""circle"", ""left"": 0, ""top"": 40 } ] } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_RepeatedItemNames_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""name"": ""S"", ""number"": 9, ""items"": [
                { ""type"": ""text"", ""name"": ""Same"", ""left"": 0, ""top"": 0, ""text"": ""A"" },
                { ""type"": ""text"", ""name"": ""Same"", ""left"": 0, ""top"": 30, ""text"": ""B"" } ] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>The Openness help: a screen imported without a number gets a free one.</remarks>
        [TestMethod]
        public void Build_ScreenWithoutNumber_LeavesTheNumberToTiaPortal()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [] }");

            Assert.IsFalse(HmiScreenDocument.Build(screen).Contains("<Number>"));
        }

        [TestMethod]
        public void Parse_ScreenWithoutName_ThrowsInvalidParams()
        {
            var failure = Refusal(@"{ ""number"": 3, ""items"": [] }");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_NotJson_ThrowsInvalidParams()
        {
            var failure = Refusal("principal: two buttons");

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_ItemsWithoutNames_AreNamedAsTiaPortalNamesNewItems()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [
                { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""A"", ""press"": ""setTag"", ""tag"": ""T"" },
                { ""type"": ""pilotLight"", ""left"": 0, ""top"": 40, ""tag"": ""T"" },
                { ""type"": ""button"", ""left"": 0, ""top"": 80, ""text"": ""B"", ""press"": ""setTag"", ""tag"": ""T"" } ] }");

            CollectionAssert.AreEqual(
                new[] { "Button_1", "Circle_1", "Button_2" },
                screen.Items.Select(item => item.Frame.Name).ToArray());
        }
    }
}
