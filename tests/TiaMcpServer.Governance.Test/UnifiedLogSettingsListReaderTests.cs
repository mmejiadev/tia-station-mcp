using System;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>Hours above 23 and minutes or seconds above 59 were refused by TIA Portal V20 on 2026-10-30.</remarks>
    [TestClass]
    public sealed class UnifiedLogSettingsListReaderTests
    {
        [TestMethod]
        public void Parse_AFullLog_ReadsEverySetting()
        {
            var request = UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""Log_Tolva"", ""maxSize"": 500, ""duration"": { ""days"": 30 },
                ""segment"": { ""maxSize"": 50, ""duration"": { ""hours"": 12 }, ""start"": ""2026-01-01T00:00:00"" }, ""backupPath"": ""D:\\Backup"" } ] }").Single();

            Assert.AreEqual(500u, request.MaxSize);
            Assert.AreEqual(30u, request.Duration!.Days);
            Assert.AreEqual(12u, request.SegmentDuration!.Hours);
            Assert.AreEqual(new DateTime(2026, 1, 1), request.SegmentStart);
            Assert.AreEqual(@"D:\Backup", request.BackupPath);
        }

        [TestMethod]
        public void Parse_APeriodGivenInPart_IsThatPeriodWhole()
        {
            var request = UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""duration"": { ""days"": 2 } } ] }").Single();

            Assert.AreEqual(2u * 86400, request.Duration!.TotalSeconds);
        }

        /// <remarks>The review of 2026-10-30: an empty request was audited as a change and reported as "settings changed".</remarks>
        [TestMethod]
        public void Parse_NothingButAName_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"" } ] }"));

            StringAssert.Contains(refusal.Message, "names no setting to change", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_OnlyASize_LeavesTheRestUnchanged()
        {
            var request = UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""maxSize"": 500 } ] }").Single();

            Assert.IsNull(request.Duration);
            Assert.IsNull(request.SegmentStart);
            Assert.IsNull(request.BackupPath);
        }

        /// <remarks>The review of 2026-10-30: a path of spaces was trimmed to "" and turned the backup off.</remarks>
        [TestMethod]
        public void Parse_ABackupPathOfSpaces_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""backupPath"": ""  "" } ] }"));

            StringAssert.Contains(refusal.Message, "only spaces", StringComparison.Ordinal);
        }

        /// <remarks>The review of 2026-10-30: a "Z" moved the start to this machine's local time.</remarks>
        [TestMethod]
        public void Parse_AStartWithATimeZone_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""segment"": { ""start"": ""2026-01-01T00:00:00Z"" } } ] }"));

            StringAssert.Contains(refusal.Message, "yyyy-MM-ddTHH:mm:ss", StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow(@"{ ""days"": 100000 }", "days", DisplayName = "100000 days, past what the plan can widen within what was measured")]
        [DataRow(@"{ ""hours"": 24 }", "hours", DisplayName = "24 hours")]
        [DataRow(@"{ ""minutes"": 60 }", "minutes", DisplayName = "60 minutes")]
        [DataRow(@"{ ""seconds"": 60 }", "seconds", DisplayName = "60 seconds")]
        public void Parse_APartTiaPortalRefuses_IsRefused(string duration, string field)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse($@"{{ ""logs"": [ {{ ""name"": ""L"", ""duration"": {duration} }} ] }}"));

            StringAssert.Contains(refusal.Message, $"'{field}'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_AZeroSize_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""maxSize"": 0 } ] }"));

            StringAssert.Contains(refusal.Message, "from 1", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ALogNamedTwice_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""maxSize"": 500 }, { ""name"": ""l"", ""maxSize"": 500 } ] }"));

            StringAssert.Contains(refusal.Message, "named twice", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_AStorageSetting_IsRefusedAsAFieldNobodyReads()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsListReader.Parse(@"{ ""logs"": [ { ""name"": ""L"", ""storage"": ""local"" } ] }"));

            StringAssert.Contains(refusal.Message, "storage", StringComparison.Ordinal);
        }
    }
}
