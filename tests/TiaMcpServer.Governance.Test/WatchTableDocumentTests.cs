using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.WatchTables;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>The document is the shape TIA Portal V21 exported after importing rows (2026-11-13).</remarks>
    [TestClass]
    public sealed class WatchTableDocumentTests
    {
        private static readonly WatchTableRow Lamp = new WatchTableRow("%Q0.0", "Bool", "TRUE", "Lamp <1> & \"2\"");
        private static readonly WatchTableRow Counter = new WatchTableRow("%MW10", "DEC_signed", string.Empty, string.Empty);

        [TestMethod]
        public void Build_ForceTable_HasOneEntryPerRowWithItsForceValue()
        {
            var document = XDocument.Parse(WatchTableDocument.Build(WatchTableKind.Force, "Force table", new[] { Lamp }, 21));

            var entry = document.Descendants("SW.WatchAndForceTables.PlcForceTableEntry").Single();
            Assert.AreEqual("%Q0.0", entry.Element("AttributeList")!.Element("Address")!.Value);
            Assert.AreEqual("TRUE", entry.Element("AttributeList")!.Element("ForceValue")!.Value);
        }

        /// <remarks>Openness refuses the document outright with it: "The property 'ForceIntention' is read-only".</remarks>
        [TestMethod]
        public void Build_ForceTable_LeavesOutTheReadOnlyIntention()
        {
            var text = WatchTableDocument.Build(WatchTableKind.Force, "Force table", new[] { Lamp }, 21);

            Assert.IsFalse(text.Contains("ForceIntention"));
        }

        [TestMethod]
        public void Build_WatchRowWithoutAValue_HasNoModifyValue()
        {
            var document = XDocument.Parse(WatchTableDocument.Build(WatchTableKind.Watch, "Checks", new[] { Counter }, 21));

            Assert.IsFalse(document.Descendants("ModifyValue").Any());
        }

        [TestMethod]
        public void Build_ForVersion21_SaysV21()
        {
            var document = XDocument.Parse(WatchTableDocument.Build(WatchTableKind.Watch, "Checks", new[] { Counter }, 21));

            Assert.AreEqual("V21", document.Root!.Element("Engineering")!.Attribute("version")!.Value);
        }

        [TestMethod]
        public void Build_ACommentWithMarkup_IsKeptAsText()
        {
            var document = XDocument.Parse(WatchTableDocument.Build(WatchTableKind.Force, "Force table", new[] { Lamp }, 21));

            Assert.AreEqual("Lamp <1> & \"2\"", document.Descendants("Text").Single().Value);
        }

        [TestMethod]
        public void Build_TwoRows_GiveEveryObjectItsOwnId()
        {
            var document = XDocument.Parse(WatchTableDocument.Build(WatchTableKind.Watch, "Checks", new[] { Lamp, Counter }, 21));

            var ids = document.Descendants().Select(element => element.Attribute("ID")?.Value).Where(id => id != null).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }
    }
}
