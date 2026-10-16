using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Spec.Tests
{
    [TestClass]
    public sealed class HmiScreenComparisonTests
    {
        private static string Reference()
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "hmi", "principal-reference.xml"));
        }

        [TestMethod]
        public void Compare_ScreenWithItself_FindsNoDifference()
        {
            var differences = HmiScreenComparison.Compare(Reference(), Reference());

            Assert.AreEqual(0, differences.Count, string.Join("\n", differences));
        }

        /// <remarks>A colour changed deep inside an animation range is exactly what a look at the screen misses.</remarks>
        [TestMethod]
        public void Compare_OneRangeColourChanged_NamesTheItem()
        {
            var changed = Reference().Replace("<BackColor>128, 128, 128</BackColor>", "<BackColor>0, 0, 255</BackColor>");

            var differences = HmiScreenComparison.Compare(Reference(), changed);

            Assert.IsTrue(differences.Count > 0 && differences.All(line => line.StartsWith("'Circle_1'", StringComparison.Ordinal)), string.Join("\n", differences));
        }

        /// <remarks>
        /// The same two colours in the other order are an inverted pilot light, not the same one: the
        /// comparison tells ranges apart by position.
        /// </remarks>
        [TestMethod]
        public void Compare_PilotLightWithItsColoursSwapped_IsADifference()
        {
            static string Light(string off, string on) => HmiScreenDocument.Build(HmiScreenDescriptionReader.Parse(
                @"{ ""name"": ""S"", ""items"": [ { ""type"": ""pilotLight"", ""name"": ""L"", ""left"": 0, ""top"": 0, ""tag"": ""T"", ""backColor"": ""red"", ""offColor"": """ + off + @""", ""onColor"": """ + on + @""" } ] }"));

            var differences = HmiScreenComparison.Compare(Light("red", "green"), Light("green", "red"));

            Assert.IsTrue(differences.Count > 0, "an inverted pilot light was reported as a match");
        }

        [TestMethod]
        public void Compare_ItemRenamed_ReportsItMissingFromTheCandidate()
        {
            var renamed = Reference().Replace("<ObjectName>Button_3</ObjectName>", "<ObjectName>Reset</ObjectName>");

            var differences = HmiScreenComparison.Compare(Reference(), renamed);

            CollectionAssert.Contains(differences.ToArray(), "'Button_3': only in the reference (Hmi.Screen.Button)");
        }
    }
}
