using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// A change to some items of a Unified screen, read before TIA Portal is touched. What is refused here is a call that
    /// would otherwise do one thing to an item where the author meant another, or nothing at all.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedScreenEditReaderTests
    {
        private const string Full = @"{ ""screen"": ""Main"",
            ""properties"": { ""BackColor"": ""#202020"" },
            ""remove"": [ ""Old"" ],
            ""set"": [ { ""name"": ""Level"", ""properties"": { ""Left"": 300, ""Font.Size"": 16 } } ],
            ""draw"": [ { ""type"": ""button"", ""name"": ""Start"", ""properties"": { ""Left"": 40 }, ""tags"": { ""Top"": ""Pos"" } } ] }";

        [TestMethod]
        public void Parse_AFullEdit_ReadsEveryPart()
        {
            var edit = UnifiedScreenEditReader.Parse(Full);

            Assert.AreEqual("Main", edit.Screen);
            Assert.AreEqual("BackColor", edit.Properties.Single().Path);
            Assert.AreEqual("Old", edit.Remove.Single());
            Assert.AreEqual(2, edit.Set.Single().Properties.Count);
            Assert.AreEqual("Start", edit.Draw.Single().Name);
            CollectionAssert.AreEqual(new[] { "Pos" }, edit.Drawing.LinkedTags.ToList());
        }

        [TestMethod]
        public void Touched_AFullEdit_NamesTheItemsRemovedAndSet()
        {
            var edit = UnifiedScreenEditReader.Parse(Full);

            CollectionAssert.AreEqual(new[] { "Old", "Level" }, edit.Touched.ToList());
        }

        [TestMethod]
        [DataRow(@"{ ""screen"": ""Main"" }", DisplayName = "nothing to do")]
        [DataRow(@"{ ""remove"": [ ""A"" ] }", DisplayName = "no screen")]
        [DataRow(@"{ ""screen"": ""Main"", ""remove"": [ 1 ] }", DisplayName = "a removal that is not a name")]
        [DataRow(@"{ ""screen"": ""Main"", ""set"": [ { ""name"": ""A"" } ] }", DisplayName = "set without properties")]
        [DataRow(@"{ ""screen"": ""Main"", ""set"": [ { ""properties"": { ""Left"": 1 } } ] }", DisplayName = "set without a name")]
        [DataRow(@"{ ""screen"": ""Main"", ""draw"": [ { ""type"": ""button"" } ] }", DisplayName = "a drawn item without a name")]
        [DataRow(@"{ ""screen"": ""Main"", ""draw"": { ""type"": ""button"" } }", DisplayName = "draw not a list")]
        [DataRow(@"{ ""screen"": ""Main"", ""draw"": [ { ""type"": ""button"", ""name"": "" "" } ] }", DisplayName = "a drawn item with a blank name")]
        [DataRow(@"{ ""screen"": ""Main"", ""set"": [ { ""name"": ""A"", ""properties"": { ""name"": ""B"" } } ] }", DisplayName = "a rename in place, refused as for any item")]
        [DataRow(@"{ ""screen"": ""Main"", ""remove"": [ ""A"" ", DisplayName = "not JSON")]
        public void Parse_AnIncompleteEdit_ThrowsInvalidParams(string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedScreenEditReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>Removed and drawn, or set and drawn: which comes first would be a guess, so the author says one.</remarks>
        [TestMethod]
        [DataRow(@"{ ""screen"": ""Main"", ""remove"": [ ""A"" ], ""draw"": [ { ""type"": ""button"", ""name"": ""a"" } ] }")]
        [DataRow(@"{ ""screen"": ""Main"", ""remove"": [ ""A"", ""A"" ] }")]
        [DataRow(@"{ ""screen"": ""Main"", ""set"": [ { ""name"": ""A"", ""properties"": { ""Left"": 1 } } ], ""remove"": [ ""A"" ] }")]
        public void Parse_AnItemNamedTwice_ThrowsInvalidParams(string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedScreenEditReader.Parse(json));

            StringAssert.Contains(failure.Message, "named once", System.StringComparison.Ordinal);
        }

        /// <remarks>A link, an event or an animation is part of what an item is drawn with: 'set' changing one would leave the others half-kept.</remarks>
        [TestMethod]
        [DataRow("tags")]
        [DataRow("events")]
        [DataRow("type")]
        public void Parse_ASetWithMoreThanProperties_ThrowsInvalidParamsNamingDraw(string key)
        {
            var json = @"{ ""screen"": ""Main"", ""set"": [ { ""name"": ""A"", ""properties"": { ""Left"": 1 }, """ + key + @""": {} } ] }";

            var failure = Assert.ThrowsException<PortalException>(() => UnifiedScreenEditReader.Parse(json));

            StringAssert.Contains(failure.Message, "draw", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void SetTextLists_ATextListSetInPlace_IsNamed()
        {
            var edit = UnifiedScreenEditReader.Parse(@"{ ""screen"": ""Main"", ""set"": [ { ""name"": ""State"", ""properties"": { ""ResourceList"": ""States"" } } ] }");

            CollectionAssert.AreEqual(new[] { "States" }, edit.SetTextLists.ToList());
        }

        [TestMethod]
        public void Parse_CommentsAndTrailingCommas_AreAllowed()
        {
            var edit = UnifiedScreenEditReader.Parse(@"{ ""screen"": ""Main"", // the start screen
                ""remove"": [ ""A"", ], }");

            Assert.AreEqual("A", edit.Remove.Single());
        }
    }
}
