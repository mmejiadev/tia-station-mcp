using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// The reference is the teacher's <c>principal</c> screen of <c>0967 - explicació clase</c>,
    /// exported through Openness on 2026-10-13 (its <c>DocumentInfo</c> block, which lists installed
    /// products, removed). The description in <c>assets/hmi</c> describes the same screen. What these
    /// tests claim is that the text matches an export; that TIA Portal imports and compiles it is for
    /// <c>Test40Hmi</c> in the TIA suite.
    /// </remarks>
    [TestClass]
    public sealed class HmiScreenGenerationTests
    {
        private static string Asset(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "hmi", name));
        }

        private static string BuildPrincipal()
        {
            return HmiScreenDocument.Build(HmiScreenDescriptionReader.Parse(Asset("principal-description.json")));
        }

        /// <remarks>
        /// The generator's done-when: the teacher's screen, attribute for attribute. What is left is
        /// exactly what it does not do on purpose — the size, which the panel decides — and what it
        /// does not do yet: the group holding a symbol-library graphic, whose content is an opaque
        /// binary blob. Any other line here is a regression.
        /// </remarks>
        [TestMethod]
        public void Build_TeachersPrincipal_DiffersOnlyInSizeAndTheSymbolLibraryGroup()
        {
            var differences = HmiScreenComparison.Compare(Asset("principal-reference.xml"), BuildPrincipal());

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Screen: reference has Height=420",
                    "Screen: reference has Width=800",
                    "'Group': only in the reference (Hmi.Screen.Group)",
                    "'Group_1': only in the reference (Hmi.Screen.Group)",
                    "'Symbol library_1': only in the reference (Hmi.Screen.SymbolLibrary)"
                },
                differences.ToArray(),
                string.Join("\n", differences));
        }

        /// <remarks>
        /// The screen overview of <c>0967 hmi_prova1</c>: a symbolic I/O field that opens the screen
        /// the operator picks, on its change event, and two date and time fields. Only the logo, an
        /// image the generator does not write, is left.
        /// </remarks>
        [TestMethod]
        public void Build_ClassScreenOverview_DiffersOnlyInTheLogo()
        {
            var built = HmiScreenDocument.Build(HmiScreenDescriptionReader.Parse(Asset("overview-description.json")));

            var differences = HmiScreenComparison.Compare(Asset("overview-reference.xml"), built)
                .Where(line => !line.StartsWith("Screen:", StringComparison.Ordinal))
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { "'Logo': only in the reference (Hmi.Screen.GraphicView)" }, differences, string.Join("\n", differences));
        }

        [TestMethod]
        public void Build_ButtonAndIoField_NumbersTheTabOrderFromOne()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [
                { ""type"": ""text"", ""left"": 0, ""top"": 0, ""text"": ""Title"" },
                { ""type"": ""button"", ""left"": 0, ""top"": 40, ""text"": ""Go"", ""press"": ""setBitWhilePressed"", ""tag"": ""Go"" },
                { ""type"": ""ioField"", ""left"": 0, ""top"": 80, ""tag"": ""Count"" } ] }");

            var tabIndexes = XDocument.Parse(HmiScreenDocument.Build(screen))
                .Descendants("TabIndex").Select(index => index.Value).ToArray();

            CollectionAssert.AreEqual(new[] { "-1", "1", "2" }, tabIndexes);
        }

        [TestMethod]
        public void Build_PilotLightWithoutVisibilityTag_WritesOnlyTheColourAnimation()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [
                { ""type"": ""pilotLight"", ""left"": 10, ""top"": 10, ""tag"": ""KM1"" } ] }");

            var animations = XDocument.Parse(HmiScreenDocument.Build(screen))
                .Descendants().Where(element => (string?)element.Attribute("CompositionName") == "Animations").ToList();

            Assert.AreEqual("Hmi.Dynamic.RangeAppearanceAnimation", animations.Single().Name.LocalName);
        }

        /// <remarks>Navigation is written as the teacher's own screens write it, parameters and all.</remarks>
        [TestMethod]
        public void Build_ActivateScreenButton_LinksTheTargetScreenByName()
        {
            var screen = HmiScreenDescriptionReader.Parse(@"{ ""name"": ""S"", ""number"": 9, ""items"": [
                { ""type"": ""button"", ""left"": 0, ""top"": 0, ""text"": ""Trends"", ""press"": ""activateScreen"", ""screen"": ""Grafics tendencia"" } ] }");

            var parameter = XDocument.Parse(HmiScreenDocument.Build(screen))
                .Descendants("Hmi.Event.FunctionListEntryParameter")
                .Single(element => element.Element("AttributeList")!.Element("Name")!.Value == "Screen name");

            Assert.AreEqual("Grafics tendencia", parameter.Element("LinkList")!.Element("Value")!.Element("Name")!.Value);
        }
    }
}
