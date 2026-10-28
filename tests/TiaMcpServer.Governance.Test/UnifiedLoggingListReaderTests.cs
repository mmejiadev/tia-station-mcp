using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Each refusal here is something TIA Portal V20 takes and only the compile refuses, measured on
    /// 2026-10-28: a logging tag with no data log, a cyclic one with no cycle, an alarm log with no class.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedLoggingListReaderTests
    {
        [TestMethod]
        public void Parse_AFullList_ReadsEveryLogAndLoggingTag()
        {
            const string Json = @"{ ""dataLogs"": [ { ""name"": ""Log_Tolva"" } ],
                ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [ ""Alarm"", ""Warning"" ] } ],
                ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""Nivell_log"", ""dataLog"": ""Log_Tolva"", ""mode"": ""cyclic"", ""cycle"": ""T1s"" } ] }";

            var list = UnifiedLoggingListReader.Parse(Json);

            CollectionAssert.AreEqual(new[] { "Log_Tolva" }, list.DataLogs.ToArray());
            CollectionAssert.AreEqual(new[] { "Alarm", "Warning" }, list.AlarmLogs.Single().Classes.ToArray());
            var logged = list.LoggingTags.Single();
            Assert.AreEqual(UnifiedLoggingMode.Cyclic, logged.Mode);
            Assert.AreEqual("T1s", logged.Cycle);
        }

        [TestMethod]
        public void Parse_ALoggingTagWithNoMode_LogsOnChange()
        {
            var list = UnifiedLoggingListReader.Parse(@"{ ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""L"", ""dataLog"": ""Log"" } ] }");

            Assert.AreEqual(UnifiedLoggingMode.OnChange, list.LoggingTags.Single().Mode);
            Assert.IsNull(list.LoggingTags.Single().Cycle);
        }

        [TestMethod]
        public void Parse_ALoggingTagWithNoDataLog_IsRefused()
        {
            AssertRefused(@"{ ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""L"" } ] }", "dataLog");
        }

        [TestMethod]
        public void Parse_ACyclicLoggingTagWithNoCycle_IsRefused()
        {
            AssertRefused(@"{ ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""L"", ""dataLog"": ""Log"", ""mode"": ""cyclic"" } ] }", "cycle");
        }

        [TestMethod]
        public void Parse_ACycleOnALoggingTagThatIsNotCyclic_IsRefused()
        {
            AssertRefused(@"{ ""loggingTags"": [ { ""tag"": ""Nivell"", ""name"": ""L"", ""dataLog"": ""Log"", ""cycle"": ""T1s"" } ] }", "only for");
        }

        [TestMethod]
        public void Parse_AnAlarmLogWithNoClass_IsRefused()
        {
            AssertRefused(@"{ ""alarmLogs"": [ { ""name"": ""Alarmes"", ""classes"": [] } ] }", "at least one alarm class");
        }

        [TestMethod]
        public void Parse_AClassInTwoAlarmLogs_IsRefused()
        {
            AssertRefused(@"{ ""alarmLogs"": [ { ""name"": ""A"", ""classes"": [ ""Alarm"" ] }, { ""name"": ""B"", ""classes"": [ ""alarm"" ] } ] }", "more than one alarm log");
        }

        [TestMethod]
        public void Parse_AClassTwiceInOneAlarmLog_IsRefusedAsSuch()
        {
            AssertRefused(@"{ ""alarmLogs"": [ { ""name"": ""A"", ""classes"": [ ""Alarm"", ""alarm"" ] } ] }", "Alarm log 'A': alarm class named twice");
        }

        /// <remarks>Measured: the same logging tag name on two HMI tags is taken; on one tag it is refused.</remarks>
        [TestMethod]
        public void Parse_OneLoggingTagNameOnTwoTags_IsAccepted()
        {
            const string Json = @"{ ""loggingTags"": [ { ""tag"": ""A"", ""name"": ""L"", ""dataLog"": ""Log"" }, { ""tag"": ""B"", ""name"": ""L"", ""dataLog"": ""Log"" } ] }";

            Assert.AreEqual(2, UnifiedLoggingListReader.Parse(Json).LoggingTags.Count);
        }

        [TestMethod]
        public void Parse_OneLoggingTagNameTwiceOnOneTag_IsRefused()
        {
            AssertRefused(@"{ ""loggingTags"": [ { ""tag"": ""A"", ""name"": ""L"", ""dataLog"": ""Log"" }, { ""tag"": ""a"", ""name"": ""l"", ""dataLog"": ""Log"" } ] }", "named twice");
        }

        [TestMethod]
        public void Parse_AnUnknownMode_IsRefusedNamingTheModes()
        {
            AssertRefused(@"{ ""loggingTags"": [ { ""tag"": ""A"", ""name"": ""L"", ""dataLog"": ""Log"", ""mode"": ""sometimes"" } ] }", "onChange");
        }

        [TestMethod]
        public void Parse_AnEmptyList_IsRefused()
        {
            AssertRefused("{ }", "at least one");
        }

        private static void AssertRefused(string json, string expected)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedLoggingListReader.Parse(json));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, expected, System.StringComparison.Ordinal);
        }
    }
}
