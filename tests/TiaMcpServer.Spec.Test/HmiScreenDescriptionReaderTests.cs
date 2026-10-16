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
