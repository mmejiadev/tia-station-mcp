using System;
using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// What these assert is that the text came out as intended. They cannot say the SCL is valid, and
    /// no test in this project can: `Test19CellPattern` in the TIA suite writes the real patterns into
    /// a real project and compiles them, and that is the test that says the patterns are correct.
    /// </remarks>
    [TestClass]
    public sealed class SclTemplateExpanderTests
    {
        private static CellSpecification TwoStations()
        {
            return new CellSpecification("Demo", new[]
            {
                new StationSpecification("Feeder", 2, 5),
                new StationSpecification("Driller", 3, 10)
            });
        }

        [TestMethod]
        public void Expand_AScalarPlaceholder_IsReplaced()
        {
            var result = SclTemplateExpander.Expand("FUNCTION_BLOCK \"FB_{{cellName}}\"", TwoStations());

            Assert.AreEqual("FUNCTION_BLOCK \"FB_Demo\"", result);
        }

        [TestMethod]
        public void Expand_AStationRegion_RepeatsOncePerStation()
        {
            var template = "VAR" + Environment.NewLine
                + "{{#stations}}" + Environment.NewLine
                + "   {{stationName}} : \"FB_Station\";" + Environment.NewLine
                + "{{/stations}}" + Environment.NewLine
                + "END_VAR";

            var result = SclTemplateExpander.Expand(template, TwoStations());

            var expected = "VAR" + Environment.NewLine
                + "   Feeder : \"FB_Station\";" + Environment.NewLine
                + "   Driller : \"FB_Station\";" + Environment.NewLine
                + "END_VAR";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Expand_ARegionOnItsOwnLines_LeavesNoBlankLineBehind()
        {
            // The tags are not content. Without this every region would leave a scar in the generated
            // SCL, and generated code a person will not read is generated code nobody checks.
            var template = "A" + Environment.NewLine
                + "{{#stations}}" + Environment.NewLine
                + "{{stationName}}" + Environment.NewLine
                + "{{/stations}}" + Environment.NewLine
                + "B";

            var result = SclTemplateExpander.Expand(template, TwoStations());

            Assert.AreEqual("A" + Environment.NewLine + "Feeder" + Environment.NewLine + "Driller" + Environment.NewLine + "B", result);
        }

        [TestMethod]
        public void Expand_TheSameRegionTwice_ExpandsBoth()
        {
            // The coordinator needs this: stations are declared in one region and called in another.
            var template = "{{#stations}}" + Environment.NewLine + "d:{{stationName}}" + Environment.NewLine + "{{/stations}}"
                + Environment.NewLine
                + "{{#stations}}" + Environment.NewLine + "c:{{stationName}}" + Environment.NewLine + "{{/stations}}";

            var result = SclTemplateExpander.Expand(template, TwoStations());

            StringAssert.Contains(result, "d:Feeder", StringComparison.Ordinal);
            StringAssert.Contains(result, "c:Driller", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Expand_AHandoverRegion_RepeatsOnceLessThanTheStations()
        {
            // The reason there are no conditionals in the template language: "the last station hands
            // over to nobody" is answered here, by the list being one shorter.
            var template = "{{#handovers}}" + Environment.NewLine + "{{fromName}}->{{toName}}" + Environment.NewLine + "{{/handovers}}";

            var result = SclTemplateExpander.Expand(template, TwoStations());

            Assert.AreEqual("Feeder->Driller" + Environment.NewLine, result);
        }

        [TestMethod]
        public void Expand_AHandoverRegion_WithOneStation_ProducesNothing()
        {
            var oneStation = new CellSpecification("Solo", new[] { new StationSpecification("Only", 1, 1) });
            var template = "{{#handovers}}" + Environment.NewLine + "{{fromName}}->{{toName}}" + Environment.NewLine + "{{/handovers}}";

            var result = SclTemplateExpander.Expand(template, oneStation);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Expand_HandoverIndices_AreOneBasedAndConsecutive()
        {
            var template = "{{#handovers}}" + Environment.NewLine + "{{fromIndex}}:{{toIndex}}" + Environment.NewLine + "{{/handovers}}";

            var result = SclTemplateExpander.Expand(template, FourStations());

            Assert.AreEqual(
                "1:2" + Environment.NewLine + "2:3" + Environment.NewLine + "3:4" + Environment.NewLine,
                result);
        }

        [TestMethod]
        public void Expand_PerStationValues_ComeFromThatStation()
        {
            var template = "{{#stations}}" + Environment.NewLine
                + "{{stationName}} {{stationIndex}} {{workSteps}} {{dwellCycles}}" + Environment.NewLine
                + "{{/stations}}";

            var result = SclTemplateExpander.Expand(template, TwoStations());

            Assert.AreEqual(
                "Feeder 1 2 5" + Environment.NewLine + "Driller 2 3 10" + Environment.NewLine,
                result);
        }

        [TestMethod]
        public void Expand_AnUnknownPlaceholder_IsRefusedAndSaysWhatItKnows()
        {
            // Left alone it would reach the SCL compiler as {{stationNmae}} and come back as a syntax
            // error in generated code, which is the least useful place to be told about a typo.
            var exception = Assert.ThrowsException<PortalException>(
                () => SclTemplateExpander.Expand("{{cellNmae}}", TwoStations()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, "cellNmae", StringComparison.Ordinal);
            StringAssert.Contains(exception.Message, "cellName", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Expand_SeveralUnknownPlaceholders_AreAllReported()
        {
            // One round trip to fix three typos, not three.
            var exception = Assert.ThrowsException<PortalException>(
                () => SclTemplateExpander.Expand("{{one}} {{two}}", TwoStations()));

            StringAssert.Contains(exception.Message, "one", StringComparison.Ordinal);
            StringAssert.Contains(exception.Message, "two", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Expand_ARegionThatIsNeverClosed_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => SclTemplateExpander.Expand("{{#stations}}" + Environment.NewLine + "x", TwoStations()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, "never closes it", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Expand_ARegionClosedWithoutBeingOpened_IsRefused()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => SclTemplateExpander.Expand("x" + Environment.NewLine + "{{/stations}}", TwoStations()));

            StringAssert.Contains(exception.Message, "without opening it", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Expand_AnEmptyTemplate_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(
                () => SclTemplateExpander.Expand(string.Empty, TwoStations()));
        }

        [TestMethod]
        public void Expand_WithNoCell_IsRejected()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => SclTemplateExpander.Expand("{{cellName}}", null!));
        }

        private static CellSpecification FourStations()
        {
            var stations = new List<StationSpecification>
            {
                new StationSpecification("Feeder", 1, 1),
                new StationSpecification("Driller", 1, 1),
                new StationSpecification("Tester", 1, 1),
                new StationSpecification("Sorter", 1, 1)
            };

            return new CellSpecification("Cell", stations);
        }
    }
}
