using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec.Tests
{
    [TestClass]
    public sealed class HmiTextListDocumentTests
    {
        private static string Asset(string name)
        {
            return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "hmi", name));
        }

        /// <summary>Removes the object numbers and the whitespace, which say nothing about the list.</summary>
        private static string Normalised(string xml)
        {
            var document = XDocument.Parse(xml);

            foreach (var element in document.Descendants())
            {
                element.Attribute("ID")?.Remove();
            }

            return document.Root!.ToString(SaveOptions.DisableFormatting);
        }

        /// <remarks>The screen-names list of <c>0967 hmi_prova1</c>, written again from its description.</remarks>
        [TestMethod]
        public void Build_ClassScreenNamesList_IsIdenticalToItsExport()
        {
            var built = HmiTextListDocument.Build(Asset("screennames-description.json"));

            Assert.AreEqual(Normalised(Asset("screennames-reference.xml")), Normalised(built.Value));
        }

        [TestMethod]
        public void Build_ThreeEntries_WritesOneSingleValueEntryEach()
        {
            var built = HmiTextListDocument.Build(@"{ ""name"": ""MODES"", ""entries"": [
                { ""value"": 0, ""text"": ""STOPPED"" }, { ""value"": 1, ""text"": ""AUTO"" }, { ""value"": 2, ""text"": ""MANUAL"" } ] }");

            var froms = XDocument.Parse(built.Value).Descendants("From").Select(from => from.Value).ToArray();
            CollectionAssert.AreEqual(new[] { "0", "1", "2" }, froms);
        }

        [TestMethod]
        public void Build_RepeatedValue_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiTextListDocument.Build(@"{ ""name"": ""MODES"", ""entries"": [
                { ""value"": 0, ""text"": ""STOPPED"" }, { ""value"": 0, ""text"": ""AUTO"" } ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        [TestMethod]
        public void Build_NoEntries_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => HmiTextListDocument.Build(@"{ ""name"": ""MODES"", ""entries"": [] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }
    }
}
