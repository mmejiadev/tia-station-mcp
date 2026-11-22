using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>Changing some members of a global data block and importing it back, through <c>EditDbMembers</c>.</summary>
    /// <remarks>
    /// On a copy of the fixture, with a data block of its own and an SCL function that reads two of its members. Measured
    /// 2026-11-22: the import replaces the block and keeps its number; it leaves the block inconsistent until the software
    /// is compiled; and it does not refuse a member a block reads, only the compile does.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test78DbMemberEdits
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string Block = "TiaMcpCell";
        private const string Reader = "TiaMcpUseCell";
        private const string Number = "777";

        private static readonly XNamespace Interface = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

        private string _testDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _testDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.RetrieveProject(Settings.Project1ArchivePath, Path.Combine(_testDirectory, "project"));

            var file = Path.Combine(_testDirectory, "cell.xml");
            File.WriteAllText(file, CellXml());
            AssemblyHooks.SharedPortal.ImportBlock(Software, string.Empty, file, Backup());
            AssemblyHooks.SharedPortal.WriteScl(Software, "FUNCTION \"" + Reader + "\" : Void\nVAR_TEMP\n    total : Real;\nEND_VAR\nBEGIN\n    #total := \"" + Block + "\".Temp + INT_TO_REAL(\"" + Block + "\".Count);\nEND_FUNCTION", Backup());
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.CompileSoftware(Software).ErrorCount, "the fixture compiles before the edit");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void EditDbMembers_ChangeAStartValueAndAddAMember_KeepsTheNumberAndTheOtherValues()
        {
            var result = Edit(@"""set"": [ { ""name"": ""Count"", ""startValue"": 9 } ], ""add"": [ { ""name"": ""Extra"", ""dataType"": ""Int"", ""startValue"": 7 } ]");

            var exported = Export();
            Assert.AreEqual("9", Start(exported, "Count"));
            Assert.AreEqual("21.5", Start(exported, "Temp"));
            Assert.AreEqual("7", Start(exported, "Extra"));
            Assert.AreEqual(Number, exported.Descendants("Number").Single().Value);
            Assert.AreEqual(0, result["compile"]!["errorsAfter"]!.GetValue<int>());
        }

        /// <remarks>Measured 2026-11-22: the import takes the removal, and only the compile reports the block that read the member.</remarks>
        [TestMethod]
        public void EditDbMembers_RemovingAMemberABlockReads_IsUndoneAndThrowsInvalidState()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""remove"": [ ""Count"" ]"));

            Assert.AreEqual(PortalErrorCode.InvalidState, failure.Code);
            StringAssert.Contains(failure.Message, "Nothing was changed", StringComparison.Ordinal);
            Assert.AreEqual("5", Start(Export(), "Count"), "the member and its start value are back");
            Assert.AreEqual(0, AssemblyHooks.SharedPortal.CompileSoftware(Software).ErrorCount);
        }

        [TestMethod]
        public void EditDbMembers_AMemberNotInTheBlock_ThrowsInvalidParamsAndChangesNothing()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""set"": [ { ""name"": ""Nope"", ""startValue"": 1 } ]"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
            Assert.AreEqual("5", Start(Export(), "Count"));
        }

        /// <remarks>Whether TIA Portal's import or its compile is what refuses a type that does not exist, the block is put back.</remarks>
        [TestMethod]
        public void EditDbMembers_ADataTypeThatDoesNotExist_PutsTheBlockBack()
        {
            var failure = Assert.ThrowsException<PortalException>(() => Edit(@"""add"": [ { ""name"": ""Bad"", ""dataType"": ""NoSuchType"" } ]"));

            StringAssert.Contains(failure.Message, "Nothing was changed", StringComparison.Ordinal);
            Assert.IsFalse(Export().Descendants(Interface + "Member").Any(member => (string?)member.Attribute("Name") == "Bad"));
        }

        private System.Text.Json.Nodes.JsonObject Edit(string body)
        {
            return AssemblyHooks.SharedPortal.EditDbMembers(Software, DbMemberEditReader.Parse(@"{ ""block"": """ + Block + @""", " + body + " }"), Backup());
        }

        private XDocument Export()
        {
            var folder = Path.Combine(_testDirectory, "export-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            AssemblyHooks.SharedPortal.ExportBlock(Software, Block, folder);

            return XDocument.Load(Path.Combine(folder, Block + ".xml"));
        }

        private static string? Start(XDocument document, string name)
        {
            return document.Descendants(Interface + "Member").First(member => (string?)member.Attribute("Name") == name).Element(Interface + "StartValue")?.Value;
        }

        private static string CellXml()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <Engineering version=""V21"" />
  <SW.Blocks.GlobalDB ID=""0"">
    <AttributeList>
      <Interface>
        <Sections xmlns=""http://www.siemens.com/automation/Openness/SW/Interface/v5"">
          <Section Name=""Static"">
            <Member Name=""Count"" Datatype=""Int""><StartValue>5</StartValue></Member>
            <Member Name=""Temp"" Datatype=""Real""><StartValue>21.5</StartValue></Member>
            <Member Name=""Flag"" Datatype=""Bool""><StartValue>true</StartValue></Member>
          </Section>
        </Sections>
      </Interface>
      <MemoryLayout>Optimized</MemoryLayout>
      <Name>" + Block + @"</Name>
      <Namespace />
      <Number>" + Number + @"</Number>
      <ProgrammingLanguage>DB</ProgrammingLanguage>
    </AttributeList>
  </SW.Blocks.GlobalDB>
</Document>";
        }

        private string Backup()
        {
            return Path.Combine(_testDirectory, "backup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }
    }
}
