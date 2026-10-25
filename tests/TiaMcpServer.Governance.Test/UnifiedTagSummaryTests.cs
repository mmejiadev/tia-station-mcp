using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Creating tags takes no backup, so the plan line is the only record of a setting: each one must be in it.</remarks>
    [TestClass]
    public sealed class UnifiedTagSummaryTests
    {
        [TestMethod]
        public void Of_InternalTag_NamesTypeAndEverySetting()
        {
            var tag = UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Level"", ""dataType"": ""Int"", ""comment"": ""Tank"", ""initialValue"": 7, ""persistent"": true,
                ""limits"": { ""lower"": 0, ""upper"": ""Level_Max"" } } ] }").Tags[0];

            Assert.AreEqual("Level (Int; comment 'Tank'; start 7; retained; limits 0..tag Level_Max)", UnifiedTagSummary.Of(tag));
        }

        [TestMethod]
        public void Of_LinkedTag_NamesLinkAndPlcReading()
        {
            var tag = UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Pressure"", ""connection"": ""HMI_Connection_1"", ""plcTag"": ""AI_Pressure"",
                ""acquisition"": { ""mode"": ""cyclicOnUse"", ""cycle"": ""T500ms"" }, ""scaling"": { ""plc"": [0, 27648], ""hmi"": [0, 10.5] },
                ""substitute"": { ""usage"": ""rangeViolation"", ""value"": 0 } } ] }").Tags[0];

            Assert.AreEqual(
                "Pressure (-> HMI_Connection_1/AI_Pressure; acquisition CyclicOnUse T500ms; scaling PLC 0..27648 to HMI 0..10.5; substitute 0 on RangeViolation)",
                UnifiedTagSummary.Of(tag));
        }

        [TestMethod]
        public void Of_PlainTag_IsNameAndType()
        {
            var tag = UnifiedTagListReader.Parse(@"{ ""tags"": [ { ""name"": ""Run"", ""dataType"": ""Bool"" } ] }").Tags[0];

            Assert.AreEqual("Run (Bool)", UnifiedTagSummary.Of(tag));
        }
    }
}
