using System;
using System.Globalization;
using System.Threading;
using Opc.Ua;

namespace TiaMcpServer.OpcUa.Test
{
    [TestClass]
    public class OpcUaValueFormatterTests
    {
        [TestMethod]
        public void FormatValue_AHalfUnderASpanishCulture_IsWrittenWithAPoint()
        {
            var original = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

            try
            {
                Assert.AreEqual("0.5", OpcUaValueFormatter.FormatValue(0.5));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void FormatValue_AnArray_IsWrittenAsAList()
        {
            Assert.AreEqual("[1, 2, 3]", OpcUaValueFormatter.FormatValue(new short[] { 1, 2, 3 }));
        }

        [TestMethod]
        public void FormatValue_ABoolean_IsWrittenInLowerCase()
        {
            Assert.AreEqual("true", OpcUaValueFormatter.FormatValue(true));
        }

        [TestMethod]
        public void Format_ABadStatus_CarriesNoValueAndNamesTheStatus()
        {
            var dataValue = new DataValue(new Variant(7), StatusCodes.BadNodeIdUnknown);

            var reading = OpcUaValueFormatter.Format("ns=3;s=\"Nope\"", dataValue);

            Assert.IsFalse(reading.IsGood);
            Assert.AreEqual(string.Empty, reading.Value);
            StringAssert.Contains(reading.Status, "BadNodeIdUnknown", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Format_ASourceTimestamp_IsWrittenAsUtcIso8601()
        {
            var dataValue = new DataValue(new Variant(1))
            {
                SourceTimestamp = new DateTime(2026, 9, 23, 10, 4, 5, 6, DateTimeKind.Utc)
            };

            var reading = OpcUaValueFormatter.Format("ns=3;s=\"X\"", dataValue);

            Assert.AreEqual("2026-09-23T10:04:05.006Z", reading.SourceTimestamp);
        }
    }
}
