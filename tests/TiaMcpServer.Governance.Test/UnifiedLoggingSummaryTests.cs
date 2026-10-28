using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Creating logs takes no backup, so the plan's line is the only record of what was asked for.</remarks>
    [TestClass]
    public sealed class UnifiedLoggingSummaryTests
    {
        [TestMethod]
        public void Of_AFullList_NamesEveryLogAndWhatEachLoggingTagLogs()
        {
            var list = UnifiedLoggingListReader.Parse(@"{ ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Alarm"" ] } ],
                ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""Nivell_log"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T1s"" } ] }");

            var summary = UnifiedLoggingSummary.Of(list);

            Assert.AreEqual("data log Log_Tolva; alarm log Alarmes (Alarm); logging tag Nivell_log (Nivell to Log_Tolva, cyclic T1s)", summary);
        }
    }
}
