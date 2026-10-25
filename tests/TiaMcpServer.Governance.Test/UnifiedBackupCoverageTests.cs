using System;
using System.Collections.Generic;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Every write is preceded by a backup of the previous state: a Unified screen holding what its backup
    /// cannot keep is named here, so it is not redrawn and lost.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedBackupCoverageTests
    {
        [TestMethod]
        public void Lost_ItemWithOnlyTagLinks_IsNothing()
        {
            var item = new FakeItem(new List<object>(), new List<object> { new TagDynamization() });

            Assert.AreEqual(0, UnifiedBackupCoverage.Lost(item, "'Tank'").Count);
        }

        [TestMethod]
        public void Lost_ItemWithAnEvent_IsNamed()
        {
            var item = new FakeItem(new List<object> { new object() }, new List<object>());

            var lost = UnifiedBackupCoverage.Lost(item, "'Start'");

            Assert.AreEqual(1, lost.Count);
            StringAssert.Contains(lost[0], "'Start' has EventHandlers", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Lost_ItemWithATagParameter_IsNamedWithItsProperty()
        {
            var item = new FakeItem(new List<object>(), new List<object> { new TagDynamization(), new TagParameterDynamization() });

            var lost = UnifiedBackupCoverage.Lost(item, "'Level'");

            Assert.AreEqual(1, lost.Count);
            StringAssert.Contains(lost[0], "'Level' has a TagParameterDynamization on Text", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Lost_ItemWhoseEventsAreKept_IsNothing()
        {
            var item = new FakeItem(new List<object> { new object() }, new List<object>());

            Assert.AreEqual(0, UnifiedBackupCoverage.Lost(item, "'Start'", eventsKept: true).Count);
        }

        [TestMethod]
        public void Lost_ItemWithPropertyEvents_IsNamedEvenWhenEventsAreKept()
        {
            var item = new FakeItemWithPropertyEvents(new List<object> { new object() });

            var lost = UnifiedBackupCoverage.Lost(item, "'Level'", eventsKept: true);

            Assert.AreEqual(1, lost.Count);
            StringAssert.Contains(lost[0], "PropertyEventHandlers", StringComparison.Ordinal);
        }

        private sealed class FakeItemWithPropertyEvents
        {
            public FakeItemWithPropertyEvents(List<object> propertyEventHandlers)
            {
                PropertyEventHandlers = propertyEventHandlers;
            }

            public List<object> PropertyEventHandlers { get; }
        }

        [TestMethod]
        public void Lost_ItemThatRedeclaresItsEvents_IsStillRead()
        {
            var item = new RedeclaringItem(new List<string> { "Up" });

            Assert.AreEqual(1, UnifiedBackupCoverage.Lost(item, "'Start'").Count);
        }

        private sealed class TagDynamization
        {
        }

        /// <summary>Like an Openness button: its own <c>EventHandlers</c> hides the base type's.</summary>
        private sealed class RedeclaringItem : FakeBase
        {
            public RedeclaringItem(List<string> eventHandlers)
            {
                EventHandlers = eventHandlers;
            }

            public new List<string> EventHandlers { get; }
        }

        private class FakeBase
        {
            public List<object> EventHandlers { get; } = new List<object>();
        }

        private sealed class TagParameterDynamization
        {
            public string PropertyName { get; } = "Text";
        }

        private sealed class FakeItem
        {
            public FakeItem(List<object> eventHandlers, List<object> dynamizations)
            {
                EventHandlers = eventHandlers;
                Dynamizations = dynamizations;
            }

            public List<object> EventHandlers { get; }

            public List<object> Dynamizations { get; }
        }
    }
}
