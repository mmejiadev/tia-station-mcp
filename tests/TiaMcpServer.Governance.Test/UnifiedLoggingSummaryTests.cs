using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The backup holds only the classes' previous log, so the plan's line is the only record of what was created.</remarks>
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

        [TestMethod]
        public void Of_ACycleFactor_IsNamedWithTheCycle()
        {
            var list = UnifiedLoggingListReader.Parse(@"{ ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""L"", ""dataLog"": ""Log"", ""mode"": ""cyclic"", ""cycle"": ""T1s"", ""cycleFactor"": 5 } ] }");

            var summary = UnifiedLoggingSummary.Of(list);

            Assert.AreEqual("logging tag L (Nivell to Log, cyclic T1s x5)", summary);
        }
    }
}
