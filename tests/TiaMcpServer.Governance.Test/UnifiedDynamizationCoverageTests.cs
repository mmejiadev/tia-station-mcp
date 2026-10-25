using System.Collections.Generic;
using System.Drawing;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Every write is preceded by a backup of the previous state, and a Unified screen's backup is what
    /// is read back: a dynamization the reader cannot draw again must be named, never passed over. Until
    /// 2026-10-25 a tag link with a table was read as a plain link and its table lost on a redraw.
    /// The fakes carry the Openness type and property names the rule reads by reflection.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedDynamizationCoverageTests
    {
        [TestMethod]
        public void Lost_PlainTagLink_IsNothing()
        {
            var link = new TagDynamization(new MappingTable("None"));

            Assert.IsNull(UnifiedDynamizationCoverage.Lost(link));
            Assert.IsTrue(UnifiedDynamizationCoverage.IsPlainLink(link));
        }

        [TestMethod]
        public void Lost_RangeTable_IsNothingButNotAPlainLink()
        {
            var link = new TagDynamization(new MappingTable("Range", new MappingTableEntryRange(false, null)));

            Assert.IsNull(UnifiedDynamizationCoverage.Lost(link));
            Assert.IsFalse(UnifiedDynamizationCoverage.IsPlainLink(link));
        }

        /// <remarks>A bound that is not a whole number would be rounded on the way back, so the range is not kept.</remarks>
        [TestMethod]
        public void Lost_RangeWithAFractionalBound_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("Range", new MappingTableEntryRange(false, null) { From = 0.5 }));

            Assert.IsNotNull(UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        public void Lost_SingleBitTable_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("Singlebit", new MappingTableEntryBitmask(), new MappingTableEntryBitmask()));

            Assert.AreEqual("a tag link with a Singlebit table", UnifiedDynamizationCoverage.Lost(link));
            Assert.IsFalse(UnifiedDynamizationCoverage.IsPlainLink(link));
        }

        /// <remarks>Switching a single-bit table to Range keeps its bitmask entries (measured 2026-10-25), which no range reads.</remarks>
        [TestMethod]
        public void Lost_RangeTableHoldingBitmaskEntries_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("Range", new MappingTableEntryBitmask()));

            Assert.IsNotNull(UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        public void Lost_EmptyRangeTable_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("Range"));

            Assert.AreEqual("an empty range table", UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        public void Lost_FlashingRangeWithoutAColour_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("Range", new MappingTableEntryRange(true, null)));

            Assert.IsNotNull(UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        public void Lost_FlashingRangeWithItsColour_IsNothing()
        {
            var link = new TagDynamization(new MappingTable("Range", new MappingTableEntryRange(true, Color.White)));

            Assert.IsNull(UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        public void Lost_Formula_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("None")) { Converter = { IsFormulaSelected = true } };

            Assert.AreEqual("a tag link with a formula", UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        public void Lost_IndirectAddressing_IsNamed()
        {
            var link = new TagDynamization(new MappingTable("None")) { UseIndirectAddressing = true };

            Assert.AreEqual("a tag link with indirect addressing", UnifiedDynamizationCoverage.Lost(link));
        }

        [TestMethod]
        [DataRow("AutomaticTags", DisplayName = "automatic")]
        [DataRow("T1s", DisplayName = "a named cycle")]
        [DataRow("Tags", DisplayName = "listed tags")]
        public void Lost_ScriptOnANamedTrigger_IsNothing(string trigger)
        {
            Assert.IsNull(UnifiedDynamizationCoverage.Lost(new ScriptDynamization(trigger)));
        }

        /// <remarks>Anything not foreseen refuses: a trigger the description cannot name, or one a later Openness adds.</remarks>
        [TestMethod]
        [DataRow("CustomCycle")]
        [DataRow("T42s")]
        public void Lost_ScriptOnAnUnlistedTrigger_IsNamed(string trigger)
        {
            Assert.AreEqual($"a script run on the trigger '{trigger}'", UnifiedDynamizationCoverage.Lost(new ScriptDynamization(trigger)));
        }

        [TestMethod]
        public void Lost_Flashing_IsNothing()
        {
            Assert.IsNull(UnifiedDynamizationCoverage.Lost(new FlashingDynamization()));
        }

        [TestMethod]
        public void Lost_TextList_IsNothing()
        {
            Assert.IsNull(UnifiedDynamizationCoverage.Lost(new ResourceListDynamization("State", "States")));
        }

        [TestMethod]
        [DataRow("", "States", DisplayName = "no tag")]
        [DataRow("State", "", DisplayName = "no list")]
        public void Lost_TextListMissingAPart_IsNamed(string tag, string list)
        {
            Assert.AreEqual("a text list missing its tag or its list", UnifiedDynamizationCoverage.Lost(new ResourceListDynamization(tag, list)));
        }

        /// <remarks>Anything not foreseen refuses: a type the reader does not draw, or one a later Openness adds, counts as lost.</remarks>
        [TestMethod]
        public void Lost_UnknownDynamizationType_IsNamedByItsType()
        {
            Assert.AreEqual("a TagParameterDynamization", UnifiedDynamizationCoverage.Lost(new TagParameterDynamization()));
        }

        private sealed class TagDynamization
        {
            public TagDynamization(MappingTable table)
            {
                Converter = new ValueConverter(table);
            }

            public bool UseIndirectAddressing { get; set; }

            public ValueConverter Converter { get; }

            public ValueConverter ValueConverter => Converter;
        }

        private sealed class ValueConverter
        {
            public ValueConverter(MappingTable table)
            {
                MappingTable = table;
            }

            public bool IsFormulaSelected { get; set; }

            public MappingTable MappingTable { get; }
        }

        private sealed class MappingTable
        {
            public MappingTable(string conditionType, params object[] entries)
            {
                ConditionType = conditionType;
                Entries = new List<object>(entries);
            }

            public string ConditionType { get; }

            public List<object> Entries { get; }
        }

        private sealed class MappingTableEntryRange
        {
            public MappingTableEntryRange(bool flashing, Color? alternateValue)
            {
                Flashing = flashing;
                AlternateValue = alternateValue;
            }

            public object From { get; set; } = 0;

            public object To { get; } = 0;

            public bool Flashing { get; }

            public object? AlternateValue { get; }
        }

        private sealed class MappingTableEntryBitmask
        {
        }

        private sealed class ScriptDynamization
        {
            public ScriptDynamization(string triggerType)
            {
                Trigger = new Trigger(triggerType);
            }

            public Trigger Trigger { get; }
        }

        private sealed class Trigger
        {
            public Trigger(string type)
            {
                Type = type;
            }

            public string Type { get; }
        }

        private sealed class FlashingDynamization
        {
        }

        private sealed class ResourceListDynamization
        {
            public ResourceListDynamization(string tag, string resourceList)
            {
                Tag = tag;
                ResourceList = resourceList;
            }

            public string Tag { get; }

            public string ResourceList { get; }
        }

        private sealed class TagParameterDynamization
        {
        }
    }
}
