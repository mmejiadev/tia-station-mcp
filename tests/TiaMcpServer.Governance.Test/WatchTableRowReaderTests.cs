using System;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.WatchTables;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// TIA Portal checks little of what a table import holds (it took %M5.0 into the force table), so these rules are
    /// the only check there is. Each test names one.
    /// </remarks>
    [TestClass]
    public sealed class WatchTableRowReaderTests
    {
        [TestMethod]
        public void Parse_ForceRowsOnInputsAndOutputs_AreReadWithTheirValues()
        {
            var rows = WatchTableRowReader.Parse("{\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\", \"value\": \"true\", \"comment\": \"Lamp\"}, {\"address\": \"%IW4\", \"format\": \"DEC_signed\", \"value\": -12}]}", WatchTableKind.Force);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("TRUE", rows[0].Value);
            Assert.AreEqual("-12", rows[1].Value);
        }

        [TestMethod]
        [DataRow("%M5.0")]
        [DataRow("%MW10")]
        [DataRow("%MD4")]
        public void Parse_ForceRowOutsideInputsAndOutputs_ThrowsInvalidParams(string address)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => WatchTableRowReader.Parse($"{{\"rows\": [{{\"address\": \"{address}\", \"format\": \"{(address.Contains(".") ? "Bool" : "DEC_signed")}\", \"value\": \"1\"}}]}}", WatchTableKind.Force));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "cannot be forced", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_ForceRowWithoutAValue_ThrowsInvalidParams()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => WatchTableRowReader.Parse("{\"rows\": [{\"address\": \"%Q0.0\", \"format\": \"Bool\"}]}", WatchTableKind.Force));

            StringAssert.Contains(refusal.Message, "has no value", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Parse_WatchRowWithoutAValue_OnlyWatches()
        {
            var rows = WatchTableRowReader.Parse("{\"rows\": [{\"address\": \"%MW10\", \"format\": \"DEC_signed\"}]}", WatchTableKind.Watch);

            Assert.AreEqual(string.Empty, rows.Single().Value);
        }

        [TestMethod]
        [DataRow("%MW2", "Hex", "16#0C")]
        [DataRow("%MD4", "Float", "1.5")]
        [DataRow("%QB1:P", "DEC_unsigned", "12")]
        [DataRow("%MW2", "DEC_signed", "-32768")]
        [DataRow("%MB2", "DEC_unsigned", "255")]
        [DataRow("%MW2", "Hex", "16#000FFFF")]
        public void Parse_WatchRowOfAMeasuredShape_IsAccepted(string address, string format, string value)
        {
            var rows = WatchTableRowReader.Parse($"{{\"rows\": [{{\"address\": \"{address}\", \"format\": \"{format}\", \"value\": \"{value}\"}}]}}", WatchTableKind.Watch);

            Assert.AreEqual(address, rows.Single().Address);
        }

        [TestMethod]
        [DataRow("\"Motor_Run\"", "Bool", "TRUE")]
        [DataRow("%Q0.8", "Bool", "TRUE")]
        [DataRow("%QW2", "Bool", "TRUE")]
        [DataRow("%Q0.0", "DEC_signed", "1")]
        [DataRow("%QW2", "DEC_signed", "abc")]
        [DataRow("%QD2", "Float", "1,5")]
        [DataRow("%MW10", "Float", "1.5")]
        [DataRow("%MD4", "Float", "1.0e300")]
        [DataRow("%QB2", "DEC_signed", "100000")]
        [DataRow("%MW2", "DEC_signed", "-32769")]
        [DataRow("%MB1", "DEC_unsigned", "256")]
        [DataRow("%MW2", "Hex", "16#10000")]
        [DataRow("%MW2:P", "DEC_signed", "1")]
        [DataRow("%QW2", "Octal", "1")]
        [DataRow("%DB1.DBX0.0", "Bool", "TRUE")]
        [DataRow("%DB1.DBW2", "DEC_signed", "1")]
        public void Parse_WatchRowThatDoesNotFit_ThrowsInvalidParams(string address, string format, string value)
        {
            var json = $"{{\"rows\": [{{\"address\": {(address.StartsWith("\"", StringComparison.Ordinal) ? address : "\"" + address + "\"")}, \"format\": \"{format}\", \"value\": \"{value}\"}}]}}";

            var refusal = Assert.ThrowsException<PortalException>(() => WatchTableRowReader.Parse(json, WatchTableKind.Watch));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        /// <remarks>All or nothing, and every problem named at once rather than the first one.</remarks>
        [TestMethod]
        public void Parse_TwoBadRows_NamesBoth()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => WatchTableRowReader.Parse("{\"rows\": [{\"address\": \"%M0.0\", \"format\": \"Bool\", \"value\": \"TRUE\"}, {\"address\": \"%Q0.0\", \"format\": \"Bool\"}]}", WatchTableKind.Force));

            StringAssert.Contains(refusal.Message, "row 1", StringComparison.Ordinal);
            StringAssert.Contains(refusal.Message, "row 2", StringComparison.Ordinal);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("not json")]
        [DataRow("{\"rows\": []}")]
        [DataRow("{\"rows\": {}}")]
        [DataRow("[]")]
        public void Parse_NoRows_ThrowsInvalidParams(string json)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => WatchTableRowReader.Parse(json, WatchTableKind.Watch));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }
    }
}
