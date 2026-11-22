using System.Linq;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Measured 2026-11-18 on TIA Portal V21: a screen has one global definition area for its events and one for its
    /// script animations; written item by item, the last one won and an item with none wiped it.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedScreenGlobalsTests
    {
        [TestMethod]
        public void Parse_TwoItemsWithTheirOwnDefinitions_GivesBothTheMerge()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""script"": ""x();"", ""globalDefinitions"": ""let a = 1;"" } ] },
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""script"": ""y();"", ""globalDefinitions"": ""let b = 2;"" } ] },
                { ""type"": ""button"", ""name"": ""C"", ""events"": [ { ""event"": ""tapped"", ""script"": ""z();"" } ] } ] }");

            var globals = screen.Items.Select(item => item.Events.Single().GlobalDefinitions).Distinct().ToList();

            Assert.AreEqual(1, globals.Count, "every event carries the one area, the item without any included");
            StringAssert.Contains(globals[0], "let a = 1;", System.StringComparison.Ordinal);
            StringAssert.Contains(globals[0], "let b = 2;", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Merge_TheSamePartTwice_IsKeptOnce()
        {
            var merged = UnifiedScreenGlobals.Merge(null, new[] { "let a = 1;", null, " ", "let a = 1;", "let c = 3;" });

            Assert.AreEqual("let a = 1;" + System.Environment.NewLine + "let c = 3;", merged);
        }

        /// <remarks>Review of 2026-11-18: a part inside another was dropped, though it said something else (`const LIMIT = 10` in `const LIMIT = 100;`).</remarks>
        [TestMethod]
        public void Merge_APartInsideTheAreaButNotWhole_IsKept()
        {
            var merged = UnifiedScreenGlobals.Merge("const LIMIT = 100;", new[] { "const LIMIT = 10" });

            StringAssert.EndsWith(merged, "const LIMIT = 10", System.StringComparison.Ordinal);
        }

        /// <remarks>Review of 2026-11-18: an edit could only add; the area read back and changed now replaces it.</remarks>
        [TestMethod]
        public void Merge_APartHoldingTheWholeArea_ReplacesIt()
        {
            var merged = UnifiedScreenGlobals.Merge("let a = 1;", new[] { "let a = 1;\nlet b = 2;" });

            Assert.AreEqual("let a = 1;" + System.Environment.NewLine + "let b = 2;", merged);
        }

        [TestMethod]
        public void Shared_WhatIsThereAlready_ComesFirst()
        {
            var items = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""script"": ""y();"", ""globalDefinitions"": ""let b = 2;"" } ] } ] }").Items;

            var shared = UnifiedScreenGlobals.Shared(items, ("import * as lib from 'Lib';", null));

            StringAssert.StartsWith(shared.Single().Events.Single().GlobalDefinitions, "import * as lib from 'Lib';", System.StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ScriptAnimationsWithTheirOwnDefinitions_ShareTheirAreaApartFromTheEvents()
        {
            var screen = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""rectangle"", ""name"": ""R1"", ""animations"": [ { ""property"": ""Left"", ""script"": ""return 1;"", ""globalDefinitions"": ""let p = 1;"" } ] },
                { ""type"": ""rectangle"", ""name"": ""R2"", ""animations"": [ { ""property"": ""Left"", ""script"": ""return 2;"", ""globalDefinitions"": ""let q = 2;"" } ],
                  ""events"": [ { ""event"": ""tapped"", ""script"": ""z();"" } ] } ] }");

            var animations = screen.Items.Select(item => ((UnifiedScriptAnimation)item.Animations.Single()).GlobalDefinitions).Distinct().ToList();

            Assert.AreEqual(1, animations.Count);
            Assert.IsNull(screen.Items[1].Events.Single().GlobalDefinitions, "the events' area holds nothing of the animations'");
        }

        /// <remarks>Review of 2026-11-22: a redraw with no event anywhere wrote TIA Portal's default over the screen's own events' definitions.</remarks>
        [TestMethod]
        public void HoldsEvents_ItemsWithoutAnyEvent_IsFalse()
        {
            var items = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""name"": ""A"" }, { ""type"": ""button"", ""name"": ""B"" } ] }").Items;

            Assert.IsFalse(UnifiedScreenGlobals.HoldsEvents(items), "with no event the area is not written, so the screen's own definitions stay");
        }

        [TestMethod]
        public void HoldsEvents_OneItemWithAnEvent_IsTrue()
        {
            var items = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""name"": ""A"" },
                { ""type"": ""button"", ""name"": ""B"", ""events"": [ { ""event"": ""tapped"", ""script"": ""y();"" } ] } ] }").Items;

            Assert.IsTrue(UnifiedScreenGlobals.HoldsEvents(items));
        }

        [TestMethod]
        public void EventDefinitions_SharedItems_GivesTheMergedAreaOnce()
        {
            var items = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""script"": ""x();"" } ] } ] }").Items;

            var shared = UnifiedScreenGlobals.Shared(items, ("let a = 1;", null));

            Assert.AreEqual("let a = 1;", UnifiedScreenGlobals.EventDefinitions(shared));
        }

        [TestMethod]
        public void EventDefinitions_NoScriptCarriesAny_IsNull()
        {
            var items = UnifiedScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""items"": [
                { ""type"": ""button"", ""name"": ""A"", ""events"": [ { ""event"": ""tapped"", ""script"": ""x();"" } ] } ] }").Items;

            Assert.IsNull(UnifiedScreenGlobals.EventDefinitions(items));
        }
    }
}
