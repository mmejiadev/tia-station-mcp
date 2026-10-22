using System;
using System.Drawing;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The value types are the ones WinCC Unified screen items use (counted from Openness V20). A value
    /// that does not fit is refused, never clipped or guessed.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedPropertyConverterTests
    {
        private enum Fill
        {
            Solid,
            Hatched
        }

        [Flags]
        private enum Shown
        {
            None = 0,
            Active = 1,
            Shelved = 2,
            Disabled = 4
        }

        [TestMethod]
        public void Convert_Colour_BuildsOpaqueColour()
        {
            var colour = (Color)UnifiedPropertyConverter.Convert(typeof(Color), Json("\"#102030\""), "BackColor");

            Assert.AreEqual(Color.FromArgb(255, 16, 32, 48), colour);
        }

        [TestMethod]
        [DataRow(typeof(byte), "200", (byte)200)]
        [DataRow(typeof(ushort), "60000", (ushort)60000)]
        [DataRow(typeof(short), "-5", (short)-5)]
        [DataRow(typeof(uint), "4000000000", 4000000000u)]
        [DataRow(typeof(int), "-40", -40)]
        [DataRow(typeof(float), "1.5", 1.5f)]
        [DataRow(typeof(double), "0.25", 0.25)]
        [DataRow(typeof(bool), "true", true)]
        [DataRow(typeof(string), "\"Tank 1\"", "Tank 1")]
        public void Convert_FittingValue_IsOfThePropertyType(Type type, string json, object expected)
        {
            var value = UnifiedPropertyConverter.Convert(type, Json(json), "P");

            Assert.AreEqual(expected, value);
            Assert.AreEqual(type, value.GetType());
        }

        [TestMethod]
        public void Convert_EnumName_IgnoresCase()
        {
            Assert.AreEqual(Fill.Hatched, UnifiedPropertyConverter.Convert(typeof(Fill), Json("\"hatched\""), "BackFillPattern"));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(128)]
        [DataRow(255)]
        public void Convert_ColourAsDescribed_KeepsItsAlpha(int alpha)
        {
            var colour = Color.FromArgb(alpha, 10, 20, 30);

            var described = UnifiedPropertyConverter.Describe(colour)!.Value;

            Assert.AreEqual(colour.ToArgb(), ((Color)UnifiedPropertyConverter.Convert(typeof(Color), described, "BackColor")).ToArgb());
        }

        [TestMethod]
        public void Convert_FlagsAsDescribed_ReadsBackTheSameFlags()
        {
            var described = UnifiedPropertyConverter.Describe(Shown.Active | Shown.Disabled)!.Value;

            Assert.AreEqual(Shown.Active | Shown.Disabled, UnifiedPropertyConverter.Convert(typeof(Shown), described, "AlarmDefinitionViewSetup"));
        }

        [TestMethod]
        [DataRow(typeof(byte), "256", DisplayName = "above a byte")]
        [DataRow(typeof(uint), "-1", DisplayName = "negative unsigned")]
        [DataRow(typeof(int), "1.5", DisplayName = "fraction for an integer")]
        [DataRow(typeof(int), "\"10\"", DisplayName = "string for an integer")]
        [DataRow(typeof(bool), "1", DisplayName = "number for a boolean")]
        [DataRow(typeof(string), "5", DisplayName = "number for a string")]
        [DataRow(typeof(Color), "\"purple-ish\"", DisplayName = "not a colour")]
        [DataRow(typeof(Color), "\"10, 20, 30, 256\"", DisplayName = "alpha above 255")]
        [DataRow(typeof(Fill), "\"Dotted\"", DisplayName = "not an enum value")]
        [DataRow(typeof(Fill), "\"Solid, Hatched\"", DisplayName = "two values for an enum that is not flags")]
        [DataRow(typeof(Shown), "\"Active, Blinking\"", DisplayName = "an unknown flag")]
        [DataRow(typeof(Shown), "\"Active,\"", DisplayName = "an empty flag")]
        [DataRow(typeof(Uri), "\"x\"", DisplayName = "unsupported type")]
        public void Convert_ValueThatDoesNotFit_ThrowsInvalidParams(Type type, string json)
        {
            var failure = Assert.ThrowsException<PortalException>(() => UnifiedPropertyConverter.Convert(type, Json(json), "P"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Describe_Colour_IsWrittenAsTheReaderTakesIt()
        {
            var described = UnifiedPropertyConverter.Describe(Color.FromArgb(1, 2, 3));

            Assert.AreEqual("1, 2, 3", described!.Value.GetString());
            Assert.AreEqual(Color.FromArgb(1, 2, 3), UnifiedPropertyConverter.Convert(typeof(Color), described.Value, "P"));
        }

        [TestMethod]
        public void Describe_UnsupportedValue_IsNull()
        {
            Assert.IsNull(UnifiedPropertyConverter.Describe(new Uri("http://example.org")));
        }

        private static JsonElement Json(string text)
        {
            using var document = JsonDocument.Parse(text);

            return document.RootElement.Clone();
        }
    }
}
