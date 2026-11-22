using System;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <summary>
    /// Applying an edit to the export of a global data block. Measured 2026-11-22 on V21: the block imported again keeps its
    /// number and every start value its XML holds, so an edit is a change to the XML and nothing else.
    /// </summary>
    [TestClass]
    public sealed class DbMemberXmlTests
    {
        private static readonly XNamespace Interface = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

        private const string Export = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <SW.Blocks.GlobalDB ID=""0"">
    <AttributeList>
      <Interface>
        <Sections xmlns=""http://www.siemens.com/automation/Openness/SW/Interface/v5"">
          <Section Name=""Static"">
            <Member Name=""Count"" Datatype=""Int"">
              <StartValue>5</StartValue>
            </Member>
            <Member Name=""Temp"" Datatype=""Real"">
              <StartValue>21.5</StartValue>
            </Member>
            <Member Name=""Label"" Datatype=""String[10]"" />
            <Member Name=""Pair"" Datatype=""Struct"">
              <Member Name=""Left"" Datatype=""Int"" />
            </Member>
            <Member Name=""Series"" Datatype=""Array[0..1] of Int"">
              <Subelement Path=""0""><StartValue>3</StartValue></Subelement>
            </Member>
          </Section>
        </Sections>
      </Interface>
      <Name>Cell</Name>
      <Number>10</Number>
    </AttributeList>
  </SW.Blocks.GlobalDB>
</Document>";

        [TestMethod]
        public void Apply_ChangeAStartValueAndAddAMember_KeepsTheOthersAsExported()
        {
            var result = Apply(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Count"", ""startValue"": 9 } ], ""add"": [ { ""name"": ""Extra"", ""dataType"": ""Int"", ""startValue"": 7 } ] }");

            Assert.AreEqual("9", Start(result, "Count"));
            Assert.AreEqual("21.5", Start(result, "Temp"));
            Assert.AreEqual("7", Start(result, "Extra"));
            Assert.AreEqual("Int", (string?)Member(result, "Extra").Attribute("Datatype"));
        }

        [TestMethod]
        public void Apply_RemoveAMember_DropsOnlyThat()
        {
            var result = Apply(@"{ ""block"": ""Cell"", ""remove"": [ ""Temp"" ] }");

            CollectionAssert.AreEqual(new[] { "Count", "Label", "Pair", "Series" }, Names(result));
        }

        [TestMethod]
        public void Apply_ANewDataTypeWithNoStartValue_DropsTheOldStartValue()
        {
            var result = Apply(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Count"", ""dataType"": ""DInt"" } ] }");

            Assert.AreEqual("DInt", (string?)Member(result, "Count").Attribute("Datatype"));
            Assert.IsNull(Start(result, "Count"), "a start value of the old type may not fit the new one");
        }

        [TestMethod]
        public void Apply_ANameInOtherCase_FindsTheMember()
        {
            var result = Apply(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""count"", ""startValue"": 1 } ] }");

            Assert.AreEqual("1", Start(result, "Count"));
        }

        [TestMethod]
        public void Apply_AMemberThatIsNotThere_ThrowsInvalidParamsListingTheMembers()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Apply(@"{ ""block"": ""Cell"", ""remove"": [ ""Nope"" ] }"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            StringAssert.Contains(failure.Message, "Count, Temp, Label", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Apply_AMemberAddedThatIsThere_ThrowsInvalidParams()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Apply(@"{ ""block"": ""Cell"", ""add"": [ { ""name"": ""Temp"", ""dataType"": ""Real"" } ] }"));

            StringAssert.Contains(failure.Message, "already", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Apply_AStruct_IsRefused()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Apply(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Pair"", ""startValue"": 1 } ] }"));

            StringAssert.Contains(failure.Message, "struct", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Apply_AStartValueOnAnArrayWithPerElementValues_IsRefused()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Apply(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Series"", ""startValue"": 1 } ] }"));

            StringAssert.Contains(failure.Message, "per element", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Apply_TheBlockNumberAndTheDeclaration_StayAsExported()
        {
            var text = DbMemberXml.Apply(Export, DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Count"", ""startValue"": 9 } ] }"));

            StringAssert.StartsWith(text, "<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal);
            StringAssert.Contains(text, "<Number>10</Number>", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Apply_AStartValueHoldingMarkup_IsEscapedNotWrittenAsMarkup()
        {
            var result = Apply(@"{ ""block"": ""Cell"", ""set"": [ { ""name"": ""Label"", ""startValue"": ""'a<b>&c'"" } ] }");

            Assert.AreEqual("'a<b>&c'", Start(result, "Label"));
            Assert.AreEqual(1, result.Descendants(Interface + "Member").Count(member => (string?)member.Attribute("Name") == "Label"));
        }

        [TestMethod]
        public void Apply_ABlockWithNoStaticSection_ThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => DbMemberXml.Apply("<Document />", DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""remove"": [ ""Count"" ] }")));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        [TestMethod]
        public void Apply_ADoctypeInTheXml_IsRefusedNotExpanded()
        {
            var hostile = "<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY x \"y\">]><Document />";

            var failure = Assert.ThrowsException<PortalException>(() => DbMemberXml.Apply(hostile, DbMemberEditReader.Parse(@"{ ""block"": ""Cell"", ""remove"": [ ""Count"" ] }")));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
        }

        private static XDocument Apply(string json)
        {
            return XDocument.Parse(DbMemberXml.Apply(Export, DbMemberEditReader.Parse(json)));
        }

        private static XElement Member(XDocument document, string name)
        {
            return document.Descendants(Interface + "Member").First(member => (string?)member.Attribute("Name") == name);
        }

        private static string? Start(XDocument document, string name)
        {
            return Member(document, name).Element(Interface + "StartValue")?.Value;
        }

        private static string[] Names(XDocument document)
        {
            return document.Descendants(Interface + "Section").First().Elements(Interface + "Member").Select(member => (string)member.Attribute("Name")!).ToArray();
        }
    }
}
