using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    [TestClass]
    public sealed class HmiEventTests
    {
        private static XDocument Build(string item)
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [ " + item + " ] }");

            return XDocument.Parse(HmiScreenDocument.Build(screen));
        }

        /// <remarks>Two functions on one click run in the order written, under one event.</remarks>
        [TestMethod]
        public void Build_TwoFunctionsOnClick_WritesOneEventWithBothInOrder()
        {
            var screen = Build(@"{ ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Manual"", ""events"": [
                { ""event"": ""click"", ""function"": ""setTag"", ""tag"": ""Mode"", ""value"": 2 },
                { ""event"": ""click"", ""function"": ""activateScreen"", ""screen"": ""Manual"" } ] }");

            var events = screen.Descendants("Hmi.Event.Event").ToList();
            var functions = events.Single().Descendants("Hmi.Event.FunctionListEntry")
                .Select(entry => entry.Element("AttributeList")!.Element("Name")!.Value)
                .ToArray();

            Assert.AreEqual("Click", events.Single().Element("AttributeList")!.Element("Name")!.Value);
            CollectionAssert.AreEqual(new[] { "SetTag", "ActivateScreen" }, functions);
        }

        [TestMethod]
        public void Build_PressShortFormAndClickList_WritesTwoEvents()
        {
            var screen = Build(@"{ ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Go"", ""press"": ""setBitWhilePressed"", ""tag"": ""Go"",
                ""events"": [ { ""event"": ""click"", ""function"": ""activatePreviousScreen"" } ] }");

            var names = screen.Descendants("Hmi.Event.Event").Select(element => element.Element("AttributeList")!.Element("Name")!.Value).ToArray();

            CollectionAssert.AreEqual(new[] { "Press", "Click" }, names);
        }

        /// <remarks>The class back button: the function takes no parameter at all.</remarks>
        [TestMethod]
        public void Build_ActivatePreviousScreen_WritesNoParameter()
        {
            var screen = Build(@"{ ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Back"", ""events"": [ { ""event"": ""click"", ""function"": ""activatePreviousScreen"" } ] }");

            Assert.AreEqual(0, screen.Descendants("Hmi.Event.FunctionListEntryParameter").Count());
        }

        /// <remarks>Change was measured on a symbolic field only; on a button it is refused before TIA Portal sees it.</remarks>
        [TestMethod]
        public void Parse_ChangeEventOnAButton_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiScreenDescriptionReader.Parse(
                @"{ ""name"": ""S"", ""items"": [ { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""X"", ""events"": [ { ""event"": ""change"", ""function"": ""activatePreviousScreen"" } ] } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>An I/O field takes no events here; a press given to one would have been dropped without a word.</remarks>
        [TestMethod]
        public void Parse_PressOnAnIoField_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiScreenDescriptionReader.Parse(
                @"{ ""name"": ""S"", ""items"": [ { ""type"": ""ioField"", ""left"": 0, ""top"": 0, ""tag"": ""T"", ""press"": ""activateScreen"", ""screen"": ""Main"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Parse_UnknownFunction_ThrowsInvalidParamsListingTheKnownOnes()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiScreenDescriptionReader.Parse(
                @"{ ""name"": ""S"", ""number"": 9, ""items"": [ { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""X"", ""events"": [ { ""event"": ""click"", ""function"": ""stopRuntime"" } ] } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "activatePreviousScreen", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_UnknownEvent_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiScreenDescriptionReader.Parse(
                @"{ ""name"": ""S"", ""number"": 9, ""items"": [ { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""X"", ""events"": [ { ""event"": ""hover"", ""function"": ""activatePreviousScreen"" } ] } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
