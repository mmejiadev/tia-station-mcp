using System.Globalization;
using System.Threading;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// What GetDeviceParameters prints is what a caller copies into SetDeviceParameter. These tests
    /// run under a Spanish culture because that is the machine the mistake was found on, and the
    /// culture is put back afterwards so no other test inherits it.
    /// </remarks>
    [TestClass]
    public sealed class ParameterValueFormatterTests
    {
        [TestMethod]
        public void Format_ADecimalOnASpanishMachine_WritesAPoint()
        {
            var original = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

                var formatted = ParameterValueFormatter.Format(0.5d);

                Assert.AreEqual("0.5", formatted);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void Format_WhatItPrints_ParsesBackToTheSameValue()
        {
            var original = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

                var parsed = ParameterValueParser.Parse(1.0d, ParameterValueFormatter.Format(0.5d)!, "Threshold");

                Assert.AreEqual(0.5d, parsed);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void Format_NoValue_ReturnsNull()
        {
            var formatted = ParameterValueFormatter.Format(null);

            Assert.IsNull(formatted);
        }
    }
}
