using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The HMI here holds data log <c>Existing</c>, alarm log <c>Alarmes</c>, the classes <c>Alarm</c> (empty),
    /// <c>Warning</c> (logged to <c>Alarmes</c>) and <c>Notice</c> (naming <c>Gone</c>, an alarm log that was
    /// deleted), and HMI tag <c>Level</c> with logging tag <c>Level_log</c> into <c>Removed</c>, a data log that
    /// was deleted. Deleting a log leaves its name in place, measured on TIA Portal V20 on 2026-10-28.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedLoggingRulesTests
    {
        [TestMethod]
        public void Problems_ALogNameTakenInAnyCase_IsAConflict()
        {
            var (_, conflicts) = Problems(@"{ ""dataLogs"": [ { ""name"": ""ALARMES"" } ] }");

            StringAssert.Contains(conflicts.Single(), "named 'ALARMES' already exists", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_AClassThatDoesNotExist_IsMissing()
        {
            var (missing, _) = Problems(@"{ ""alarmLogs"": [ { ""name"": ""New"", ""classes"": [ ""No_Such_Class"" ] } ] }");

            StringAssert.Contains(missing.Single(), "'No_Such_Class', which does not exist", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_AClassAlreadyLogged_IsNotMoved()
        {
            var (_, conflicts) = Problems(@"{ ""alarmLogs"": [ { ""name"": ""New"", ""classes"": [ ""warning"" ] } ] }");

            StringAssert.Contains(conflicts.Single(), "'warning' is already logged to 'Alarmes'", StringComparison.Ordinal);
        }

        /// <remarks>The JSON reader refuses this first; a list built in code must meet it here too.</remarks>
        [TestMethod]
        public void Problems_ADataLogAndAnAlarmLogOfOneName_IsAConflict()
        {
            var list = new UnifiedLoggingList(new[] { "Same" }, new[] { new UnifiedAlarmLogDescription("same", new[] { "Alarm" }) }, Array.Empty<UnifiedLoggingTag>());

            var (_, conflicts) = new UnifiedLoggingRules(Hmi()).Problems(list);

            StringAssert.Contains(conflicts.Single(), "share one set of names", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_AClassNamingADeletedLog_IsAConflict()
        {
            var (_, conflicts) = Problems(@"{ ""alarmLogs"": [ { ""name"": ""New"", ""classes"": [ ""Notice"" ] } ] }");

            StringAssert.Contains(conflicts.Single(), "names alarm log 'Gone', which was deleted", StringComparison.Ordinal);
        }

        /// <remarks>Notice is not in the list, yet creating Gone again would log it there without a word.</remarks>
        [TestMethod]
        public void Problems_RecreatingAnAlarmLogAClassStillNames_IsAConflict()
        {
            var (_, conflicts) = Problems(@"{ ""alarmLogs"": [ { ""name"": ""gone"", ""classes"": [ ""Alarm"" ] } ] }");

            StringAssert.Contains(conflicts.Single(), "alarm class 'Notice' still names alarm log 'Gone'", StringComparison.Ordinal);
        }

        /// <remarks>Level_log is not in the list, yet creating Removed again would log Level there without a word.</remarks>
        [TestMethod]
        public void Problems_RecreatingADataLogALoggingTagStillNames_IsAConflict()
        {
            var (_, conflicts) = Problems(@"{ ""dataLogs"": [ { ""name"": ""removed"" } ] }");

            StringAssert.Contains(conflicts.Single(), "logging tag 'Level_log' of tag 'Level' still names data log 'Removed'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_ATagThatDoesNotExist_IsMissing()
        {
            var (missing, _) = Problems(@"{ ""loggingTags"": [ { ""tag"": ""No_Such_Tag"", ""name"": ""L"", ""dataLog"": ""Existing"" } ] }");

            StringAssert.Contains(missing.Single(), "tag 'No_Such_Tag', which does not exist", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_ADataLogThatDoesNotExist_IsMissing()
        {
            var (missing, _) = Problems(@"{ ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""L"", ""dataLog"": ""Removed"" } ] }");

            StringAssert.Contains(missing.Single(), "data log 'Removed', which does not exist", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_ALoggingTagNameTakenOnItsTagInAnyCase_IsAConflict()
        {
            var (_, conflicts) = Problems(@"{ ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""LEVEL_LOG"", ""dataLog"": ""Existing"" } ] }");

            StringAssert.Contains(conflicts.Single(), "already has a logging tag named 'LEVEL_LOG'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Problems_AListThatFits_HasNone()
        {
            var (missing, conflicts) = Problems(@"{ ""dataLogs"": [ { ""name"": ""New"" } ],
                ""alarmLogs"": [ { ""name"": ""More"", ""classes"": [ ""alarm"" ] } ],
                ""loggingTags"": [ { ""tag"": ""Level"", ""name"": ""Other"", ""dataLog"": ""new"" } ] }");

            Assert.AreEqual(0, missing.Count + conflicts.Count, string.Join("; ", missing.Concat(conflicts)));
        }

        private static (List<string> Missing, List<string> Conflicts) Problems(string json)
        {
            return new UnifiedLoggingRules(Hmi()).Problems(UnifiedLoggingListReader.Parse(json));
        }

        private static UnifiedLoggingState Hmi()
        {
            var classLogs = new Dictionary<string, string> { ["Alarm"] = string.Empty, ["Warning"] = "Alarmes", ["Notice"] = "Gone" };

            return new UnifiedLoggingState(
                (new[] { "Existing" }, new[] { "Alarmes" }),
                classLogs,
                new[] { "Level" },
                new[] { (Tag: "Level", Name: "Level_log", DataLog: "Removed") });
        }
    }
}
