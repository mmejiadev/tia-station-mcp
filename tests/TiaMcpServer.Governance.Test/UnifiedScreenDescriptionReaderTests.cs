using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The description is checked before TIA Portal is touched, so every way it can be wrong is refused
    /// here, with nothing drawn.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedScreenDescriptionReaderTests
    {
        private const string Full = @"{
            ""name"": ""Main"", ""startScreen"": true,
            ""properties"": { ""Width"": 800, ""BackColor"": ""#202020"" },
            ""items"": [
                { ""type"": ""rectangle"", ""name"": ""Tank"", ""properties"": { ""Left"": 40, ""Font.Size"": 14 } },
                { ""type"": ""HmiPolyline"", ""name"": ""Pipe"", ""points"": [[0, 0], [60, 0], [60, 90]] },
                { ""type"": ""ioField"", ""name"": ""Level"", ""tags"": { ""ProcessValue"": ""Level"" } },
                { ""type"": ""text"" }, // trailing comma and comment allowed
            ] }";

        [TestMethod]
        public void Parse_FullDescription_ReadsScreenAndItems()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(Full);

            Assert.AreEqual("Main", screen.Name);
            Assert.IsTrue(screen.IsStartScreen);
            Assert.AreEqual(2, screen.Properties.Count);
            CollectionAssert.AreEqual(new[] { "Tank", "Pipe", "Level", "text_4" }, screen.Items.Select(item => item.Name).ToArray());
        }

        [TestMethod]
        public void Parse_TypeNames_ResolveToOpennessTypes()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(Full);

            Assert.AreEqual("Siemens.Engineering.HmiUnified.UI.Shapes.HmiRectangle", screen.Items[0].TypeName);
            Assert.AreEqual("Siemens.Engineering.HmiUnified.UI.Shapes.HmiPolyline", screen.Items[1].TypeName);
            Assert.AreEqual("Siemens.Engineering.HmiUnified.UI.Widgets.HmiIOField", screen.Items[2].TypeName);
        }

        [TestMethod]
        public void Parse_PointsAndTags_AreKept()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(Full);

            Assert.AreEqual(3, screen.Items[1].Points.Count);
            Assert.AreEqual(90, screen.Items[1].Points[2].Y);
            Assert.AreEqual("Level", screen.Items[2].Tags["ProcessValue"]);
            CollectionAssert.AreEqual(new[] { "Level" }, screen.LinkedTags.ToArray());
        }

        [TestMethod]
        public void Parse_AnimationTags_AreLinkedTagsToo()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""circle"", ""tags"": { ""BorderColor"": ""Border"" }, ""animations"": [ { ""property"": ""BackColor"", ""tag"": ""Motor"", ""ranges"": [ { ""from"": 1, ""value"": ""green"" } ] } ] },
                { ""type"": ""circle"", ""animations"": [ { ""property"": ""BackColor"", ""script"": ""return 0;"", ""trigger"": [ ""Level"", ""motor"" ] } ] } ] }");

            CollectionAssert.AreEqual(new[] { "Border", "Motor", "Level" }, screen.LinkedTags.ToArray());
        }

        [TestMethod]
        public void Parse_ListsShown_AreLinkedTextLists()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""symbolicIOField"", ""properties"": { ""ResourceList"": ""States"" }, ""tags"": { ""ProcessValue"": ""State"" } },
                { ""type"": ""text"", ""animations"": [ { ""property"": ""Text"", ""tag"": ""State"", ""list"": ""Modes"" } ] },
                { ""type"": ""symbolicIOField"", ""properties"": { ""resourceList"": ""states"" } } ] }");

            CollectionAssert.AreEqual(new[] { "States", "Modes" }, screen.LinkedTextLists.ToArray());
        }

        /// <remarks>A tag link and an animation are both dynamizations; TIA Portal replaces one with the other without a word.</remarks>
        [TestMethod]
        public void Parse_PropertyLinkedAndAnimated_ThrowsInvalidParams()
        {
            var json = @"{ ""name"": ""S"", ""items"": [ { ""type"": ""circle"", ""tags"": { ""BackColor"": ""Motor"" }, ""animations"": [ { ""property"": ""backColor"", ""tag"": ""Motor"", ""ranges"": [ { ""from"": 1, ""value"": ""green"" } ] } ] } ] }";

            var failure = Assert.ThrowsException<PortalException>(() => UnifiedScreenDescriptionReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "both links and animates backColor", System.StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow(@"{ ""items"": [] }", DisplayName = "no name")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""hologram"" } ] }", DisplayName = "unknown type")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""faceplateContainer"" } ] }", DisplayName = "needs a contained type")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""rectangle"", ""name"": ""A"" }, { ""type"": ""circle"", ""name"": ""a"" } ] }", DisplayName = "repeated name")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""polyline"" } ] }", DisplayName = "polyline without points")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""polyline"", ""points"": [[0, 0]] } ] }", DisplayName = "one point")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""polyline"", ""points"": [[0, 0], [1.5, 2]] } ] }", DisplayName = "fractional point")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""rectangle"", ""points"": [[0, 0], [1, 2]] } ] }", DisplayName = "points on a rectangle")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""rectangle"", ""properties"": { ""Name"": ""X"" } } ] }", DisplayName = "name as a property")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""rectangle"", ""properties"": { ""Font..Size"": 1 } } ] }", DisplayName = "bad path")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""rectangle"", ""properties"": { ""Left"": 1, ""left"": 2 } } ] }", DisplayName = "property twice")]
        [DataRow(@"{ ""name"": ""S"", ""items"": [ { ""type"": ""ioField"", ""tags"": { ""ProcessValue"": """" } } ] }", DisplayName = "empty tag")]
        [DataRow(@"{ ""name"": ""S"", ""items"": { } }", DisplayName = "items not a list")]
        [DataRow(@"{ ""name"": ", DisplayName = "invalid JSON")]
        public void Parse_WrongDescription_ThrowsInvalidParams(string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedScreenDescriptionReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
