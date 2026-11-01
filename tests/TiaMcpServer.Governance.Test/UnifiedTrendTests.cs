using System;
using System.Collections.Generic;
using System.Drawing;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTrends;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: a time range factor outside 1 to 32767 is refused, a source names a
    /// tag and optionally its logging tag, and an unknown bare tag name is taken without a word.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedTrendTests
    {
        private const string Two = @"{ ""screen"": ""Start"", ""control"": ""Trend_1"",
            ""trends"": [ { ""tag"": ""Level"", ""loggingTag"": ""Level_log"", ""name"": ""Nivell"", ""color"": ""#FF0000"", ""mode"": ""stepped"" }, { ""tag"": ""Pressure"" } ],
            ""timeRange"": { ""factor"": 10, ""base"": ""minute"" } }";

        [TestMethod]
        public void Parse_TwoTrendsAndARange_ReadsEveryValueInOpennessSpelling()
        {
            var list = UnifiedTrendListReader.Parse(Two);

            Assert.AreEqual("Level_log", list.Trends[0].LoggingTag);
            Assert.AreEqual(Color.FromArgb(255, 0, 0).ToArgb(), list.Trends[0].Color!.Value.ToArgb());
            Assert.AreEqual("Stepped", list.Trends[0].Mode);
            Assert.IsNull(list.Trends[1].LoggingTag);
            Assert.AreEqual((10, "Minute"), list.TimeRange);
        }

        [TestMethod]
        [DataRow(0, DisplayName = "factor 0")]
        [DataRow(32768, DisplayName = "factor 32768")]
        public void Parse_AFactorTiaPortalRefuses_IsRefused(int factor)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedTrendListReader.Parse(Two.Replace("\"factor\": 10", $"\"factor\": {factor}")));

            StringAssert.Contains(refusal.Message, "1 to 32767", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_AModeTiaPortalDoesNotHave_IsRefusedNamingTheModes()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedTrendListReader.Parse(Two.Replace("stepped", "smooth")));

            StringAssert.Contains(refusal.Message, "Points, Interpolated, Stepped, Bar, Value", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ATagThatDoesNotExist_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => Rules().Check(UnifiedTrendListReader.Parse(Two.Replace("\"Pressure\"", "\"No_Such\""))));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "'No_Such' does not exist", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ALoggingTagTheTagLacks_IsRefusedNamingWhatItHas()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => Rules().Check(UnifiedTrendListReader.Parse(Two.Replace("Level_log", "Other_log"))));

            StringAssert.Contains(refusal.Message, "it has: Level_log", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_AControlAlreadyShowingATrend_IsRefusedAsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedTrendRules.RequireNoTrendSet("Trend_1", new[] { string.Empty, "Level" }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
            StringAssert.Contains(refusal.Message, "already shows Level", StringComparison.Ordinal);
        }

        [TestMethod]
        public void SourceOf_ALoggedTrendInAnotherCase_IsWrittenAsTiaPortalSpellsIt()
        {
            var trend = UnifiedTrendListReader.Parse(Two.Replace("\"tag\": \"Level\"", "\"tag\": \"level\"").Replace("Level_log", "level_LOG")).Trends[0];

            Assert.AreEqual("Level:Level_log", Rules().SourceOf(trend));
        }

        [TestMethod]
        public void RequireNoTrendSet_OnlyEmptySources_PassesSilently()
        {
            UnifiedTrendRules.RequireNoTrendSet("Trend_1", new[] { string.Empty, string.Empty });
        }

        private static UnifiedTrendRules Rules()
        {
            return new UnifiedTrendRules(new Dictionary<string, IReadOnlyList<string>>
            {
                ["Level"] = new[] { "Level_log" },
                ["Pressure"] = Array.Empty<string>()
            });
        }
    }
}
