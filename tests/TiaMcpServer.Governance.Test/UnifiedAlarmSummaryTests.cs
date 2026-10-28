using TiaMcpServer.UnifiedAlarms;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Creating alarms takes no backup, so the plan line is the only record of what was asked for.</remarks>
    [TestClass]
    public sealed class UnifiedAlarmSummaryTests
    {
        [TestMethod]
        public void Of_List_NamesEveryClassAndAlarmWithWhatRaisesIt()
        {
            var list = UnifiedAlarmListReader.Parse(@"{ ""classes"": [ { ""name"": ""Tolva"", ""priority"": 10, ""states"": { ""raised"": { ""backColor"": ""orange"", ""flashing"": true } } } ],
                ""discrete"": [ { ""name"": ""Plena"", ""tag"": ""Alarms"", ""bit"": 3, ""trigger"": ""fallingEdge"", ""class"": ""Tolva"", ""text"": ""Tolva plena"", ""info"": ""Buidar"", ""origin"": ""Tolva"", ""area"": ""Linia 1"" } ],
                ""analog"": [ { ""name"": ""Alta"", ""tag"": ""Pressure"", ""condition"": ""upperLimit"", ""value"": 8.5, ""priority"": 4, ""text"": ""Pressio alta"" } ] }");

            Assert.AreEqual(
                "class Tolva priority 10 Raised [back 255, 165, 0, flashing]; discrete Plena (Alarms bit 3 falling, Tolva, 'Tolva plena', info 'Buidar', origin 'Tolva', area 'Linia 1'); analog Alta (Pressure UpperLimit 8.5, Alarm, priority 4, 'Pressio alta')",
                UnifiedAlarmSummary.Of(list));
        }
    }
}
