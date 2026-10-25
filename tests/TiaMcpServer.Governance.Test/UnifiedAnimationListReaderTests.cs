using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The three animations a description draws, and the refusals of what TIA Portal V20 would take and
    /// never act on as written: a reversed or overlapping range, a fractional bound, a property animated
    /// twice, a field of another kind.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedAnimationListReaderTests
    {
        [TestMethod]
        public void Read_PilotLight_IsOneRangePerValue()
        {
            var animations = Read(@"[ { ""property"": ""backColor"", ""tag"": "" Motor "", ""ranges"": [ { ""from"": 0, ""value"": ""red"" }, { ""from"": 1, ""value"": ""green"" } ] } ]");

            var ranges = (UnifiedRangeAnimation)animations.Single();
            Assert.AreEqual("backColor", ranges.Path);
            Assert.AreEqual("Motor", ranges.Tag);
            Assert.AreEqual(2, ranges.Ranges.Count);
            Assert.AreEqual(1L, ranges.Ranges[1].To);
            Assert.AreEqual("green", ranges.Ranges[1].Value.GetString());
        }

        [TestMethod]
        public void Read_RangeThatFlashes_KeepsItsRateAndAlternate()
        {
            var animations = Read(@"[ { ""property"": ""BackColor"", ""tag"": ""Level"", ""ranges"": [ { ""from"": 80, ""to"": 100, ""value"": ""red"", ""flashing"": { ""rate"": ""FAST"", ""alternate"": ""white"" } } ] } ]");

            var flashing = ((UnifiedRangeAnimation)animations.Single()).Ranges.Single().Flashing!;
            Assert.AreEqual("Fast", flashing.Rate);
            Assert.AreEqual("white", flashing.Alternate.GetString());
        }

        [TestMethod]
        public void Read_Flashing_DefaultsToMediumAndAlways()
        {
            var animations = Read(@"[ { ""property"": ""BorderColor"", ""flashing"": { ""color"": ""red"", ""alternate"": ""gray"" } } ]");

            var flashing = (UnifiedFlashingAnimation)animations.Single();
            Assert.AreEqual("Medium", flashing.Rate);
            Assert.AreEqual("Always", flashing.Condition);
        }

        [TestMethod]
        [DataRow(@"{ ""property"": ""BackColor"", ""script"": ""return 0;"" }", "automatic", 0)]
        [DataRow(@"{ ""property"": ""BackColor"", ""script"": ""return 0;"", ""trigger"": ""t1S"" }", "T1s", 0)]
        [DataRow(@"{ ""property"": ""BackColor"", ""script"": ""return 0;"", ""trigger"": [ ""Motor"", ""motor"", ""Level"" ] }", "Tags", 2)]
        public void Read_ScriptTrigger_IsNamedOrListed(string entry, string trigger, int tags)
        {
            var script = (UnifiedScriptAnimation)Read($"[ {entry} ]").Single();

            Assert.AreEqual(trigger, script.Trigger);
            Assert.AreEqual(tags, script.TriggerTags.Count);
        }

        [TestMethod]
        public void Read_List_KeepsItsTagAndList()
        {
            var list = (UnifiedListAnimation)Read(@"[ { ""property"": ""Text"", ""tag"": "" State "", ""list"": "" States "" } ]").Single();

            Assert.AreEqual("State", list.Tag);
            Assert.AreEqual("States", list.List);
            CollectionAssert.AreEqual(new[] { "State" }, list.LinkedTags.ToArray());
        }

        [TestMethod]
        [DataRow(@"[ { ""property"": ""Text"", ""list"": ""States"" } ]", DisplayName = "list without a tag")]
        [DataRow(@"[ { ""property"": ""Text"", ""tag"": ""State"", ""list"": """" } ]", DisplayName = "empty list name")]
        [DataRow(@"[ { ""property"": ""Text"", ""tag"": ""State"", ""list"": ""States"", ""ranges"": [ { ""from"": 1, ""value"": ""A"" } ] } ]", DisplayName = "list and ranges")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 5, ""to"": 1, ""value"": ""red"" } ] } ]", DisplayName = "reversed range")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 0, ""to"": 10, ""value"": ""red"" }, { ""from"": 10, ""to"": 20, ""value"": ""blue"" } ] } ]", DisplayName = "overlapping ranges")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 0.5, ""value"": ""red"" } ] } ]", DisplayName = "fractional bound")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""to"": 3, ""value"": ""red"" } ] } ]", DisplayName = "no from")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 1 } ] } ]", DisplayName = "no value")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [] } ]", DisplayName = "no ranges")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""ranges"": [ { ""from"": 1, ""value"": ""red"" } ] } ]", DisplayName = "ranges without a tag")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 1, ""value"": ""red"", ""flashing"": { ""rate"": ""fast"" } } ] } ]", DisplayName = "range flashing without alternate")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 1, ""value"": ""red"", ""blink"": true } ] } ]", DisplayName = "unknown range field")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""tag"": ""M"", ""ranges"": [ { ""from"": 1, ""value"": ""red"", ""flashing"": { ""rate"": 3, ""alternate"": ""white"" } } ] } ]", DisplayName = "range rate not a name")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""flashing"": { ""color"": ""red"" } } ]", DisplayName = "flashing without alternate")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""flashing"": { ""color"": ""red"", ""alternate"": ""gray"", ""rate"": ""warp"" } } ]", DisplayName = "unknown rate")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""flashing"": { ""color"": ""red"", ""alternate"": ""gray"" }, ""tag"": ""M"" } ]", DisplayName = "tag on a flashing animation")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""script"": ""return 0;"", ""trigger"": ""CustomCycle"" } ]", DisplayName = "custom cycle")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""script"": ""return 0;"", ""trigger"": [] } ]", DisplayName = "empty trigger list")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""script"": """" } ]", DisplayName = "empty script")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""script"": ""return 0;"", ""flashing"": { ""color"": ""red"", ""alternate"": ""gray"" } } ]", DisplayName = "two kinds")]
        [DataRow(@"[ { ""property"": ""BackColor"" } ]", DisplayName = "no kind")]
        [DataRow(@"[ { ""tag"": ""M"", ""ranges"": [ { ""from"": 1, ""value"": ""red"" } ] } ]", DisplayName = "no property")]
        [DataRow(@"[ { ""property"": ""Name"", ""script"": ""return 0;"" } ]", DisplayName = "not a property")]
        [DataRow(@"[ { ""property"": ""BackColor"", ""script"": ""return 0;"" }, { ""property"": ""backcolor"", ""script"": ""return 1;"" } ]", DisplayName = "property twice")]
        [DataRow(@"{ ""property"": ""BackColor"" }", DisplayName = "animations not a list")]
        public void Read_Ambiguous_ThrowsInvalidParams(string animations)
        {
            var failure = Assert.ThrowsException<PortalException>(() => Read(animations));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        private static IReadOnlyList<UnifiedAnimation> Read(string animations)
        {
            using var document = JsonDocument.Parse($@"{{ ""animations"": {animations} }}");

            return UnifiedAnimationListReader.Read(document.RootElement, "Item 'Lamp'");
        }
    }
}
