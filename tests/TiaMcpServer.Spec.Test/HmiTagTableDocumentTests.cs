using System;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.Spec.Tests
{
    /// <remarks>
    /// The reference is the table measured to import and compile with 0 errors and 0 warnings on
    /// 2026-10-13, with its acquisition cycle changed to the one the generator writes. Whether TIA
    /// Portal still accepts it is for <c>Test40Hmi</c> in the TIA suite to say; this says the text did
    /// not drift from the shape that was measured.
    /// </remarks>
    [TestClass]
    public sealed class HmiTagTableDocumentTests
    {
        private const string MeasuredTable = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <Engineering version=""V20"" />
  <Hmi.Tag.TagTable ID=""0"">
    <AttributeList>
      <Name>Generada</Name>
    </AttributeList>
    <ObjectList>
      <Hmi.Tag.Tag ID=""1"" CompositionName=""Tags"">
        <AttributeList>
          <Name>Gen_Marxa</Name>
        </AttributeList>
        <LinkList>
          <AcquisitionCycle TargetID=""@OpenLink"">
            <Name>1 s</Name>
          </AcquisitionCycle>
          <Connection TargetID=""@OpenLink"">
            <Name>HMI_Connection_1</Name>
          </Connection>
          <ControllerTag TargetID=""@OpenLink"">
            <Name>Marxa</Name>
          </ControllerTag>
        </LinkList>
      </Hmi.Tag.Tag>
      <Hmi.Tag.Tag ID=""2"" CompositionName=""Tags"">
        <AttributeList>
          <Name>Gen_KM1</Name>
        </AttributeList>
        <LinkList>
          <AcquisitionCycle TargetID=""@OpenLink"">
            <Name>1 s</Name>
          </AcquisitionCycle>
          <Connection TargetID=""@OpenLink"">
            <Name>HMI_Connection_1</Name>
          </Connection>
          <ControllerTag TargetID=""@OpenLink"">
            <Name>KM1</Name>
          </ControllerTag>
        </LinkList>
      </Hmi.Tag.Tag>
    </ObjectList>
  </Hmi.Tag.TagTable>
</Document>";

        [TestMethod]
        public void Build_TwoTags_MatchesTheShapeMeasuredToCompile()
        {
            var links = new[] { new HmiTagLink("Gen_Marxa", "Marxa"), new HmiTagLink("Gen_KM1", "KM1") };

            var built = HmiTagTableDocument.Build("Generada", HmiTagTableDocument.DefaultConnection, links);

            Assert.IsTrue(
                XNode.DeepEquals(XDocument.Parse(MeasuredTable), XDocument.Parse(built)),
                "The generated table differs from the measured one:\n" + built);
        }

        [TestMethod]
        public void Build_NoTags_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(
                () => HmiTagTableDocument.Build("Generada", HmiTagTableDocument.DefaultConnection, Array.Empty<HmiTagLink>()));
        }

        [TestMethod]
        public void Build_EmptyConnection_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(
                () => HmiTagTableDocument.Build("Generada", " ", new[] { new HmiTagLink("Marxa", "Marxa") }));
        }

        /// <remarks>PLC tag names are free text; one with an ampersand must not break the document.</remarks>
        [TestMethod]
        public void Build_NameWithMarkupCharacters_ReadsBackUnchanged()
        {
            var built = HmiTagTableDocument.Build("Generada", HmiTagTableDocument.DefaultConnection, new[] { new HmiTagLink("A&B<1>", "A&B<1>") });

            var controllerTag = XDocument.Parse(built).Descendants("ControllerTag").Single().Element("Name")!.Value;
            Assert.AreEqual("A&B<1>", controllerTag);
        }
    }
}
