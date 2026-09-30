using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Import of blocks from SIMATIC SD documents, and above all what it says when it fails.
    /// </summary>
    /// <remarks>
    /// Until 2026-09-28 both imports caught every exception and answered with fewer blocks or false.
    /// A document that did not import, a group that did not exist and an invalid filter all came back
    /// as "completed" — and a mistyped group put the blocks at the root of the program. On a write,
    /// the caller goes on to compile and download believing the project holds what it asked for.
    ///
    /// The valid document is the one Test33DocumentExport imports, because it is measured to import.
    /// The invalid one is not a document at all, so whether TIA Portal throws on it or answers with a
    /// failed state, it must not import.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test35DocumentImport
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string NotADocumentName = "FC_NotADocument";

        private string _directory = string.Empty;
        private string _importDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _directory = AssemblyHooks.CreateTestDirectory();
            _importDirectory = Path.Combine(_directory, "import");
            Directory.CreateDirectory(_importDirectory);
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
            McpServer.Portal = AssemblyHooks.SharedPortal;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void ImportBlocksFromDocuments_AValidDocument_IsImported()
        {
            WriteValidDocument();

            var report = AssemblyHooks.SharedPortal.ImportBlocksFromDocuments(Request(string.Empty), Test33DocumentExport.BrokenName, "Override");

            Assert.IsTrue(report.Imported.Any(block => block.Name == Test33DocumentExport.BrokenName), $"The block is not among those imported. Failures: {string.Join("; ", report.Failures)}");
            Assert.AreEqual(0, report.Failures.Count, string.Join("; ", report.Failures));
        }

        /// <remarks>
        /// Through the tool, because the response is what the caller reads: the document is named in
        /// Failed and the import does not claim success. No server and no request context — what is
        /// under test is the response, not the progress notifications.
        /// </remarks>
        [TestMethod]
        public async Task ImportBlocksFromDocuments_ADocumentThatCannotBeImported_IsNamedInFailed()
        {
            WriteNotADocument();

            var response = await McpServer.ImportBlocksFromDocuments(null!, null!, Software, string.Empty, _importDirectory, NotADocumentName);

            var failures = response.Failed?.ToList() ?? new List<string>();
            Assert.IsTrue(failures.Any(line => line.StartsWith($"{NotADocumentName}:")), $"The document that did not import is not named in Failed: {response.Message}");
            Assert.AreEqual(false, response.Meta?["success"]?.GetValue<bool>(), "An import with a failed document reported success");
        }

        [TestMethod]
        public void ImportBlocksFromDocuments_AGroupThatDoesNotExist_ThrowsNotFound()
        {
            WriteValidDocument();

            var thrown = Assert.ThrowsException<PortalException>(() =>
                AssemblyHooks.SharedPortal.ImportBlocksFromDocuments(Request("NoSuchGroup"), Test33DocumentExport.BrokenName, "Override"));

            Assert.AreEqual(PortalErrorCode.NotFound, thrown.Code, thrown.Message);
        }

        [TestMethod]
        public void ImportBlocksFromDocuments_InvalidFilter_ThrowsInvalidParams()
        {
            WriteValidDocument();

            var thrown = Assert.ThrowsException<PortalException>(() =>
                AssemblyHooks.SharedPortal.ImportBlocksFromDocuments(Request(string.Empty), "[", "Override"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, thrown.Code, thrown.Message);
        }

        [TestMethod]
        public void ImportFromDocuments_ADocumentThatCannotBeImported_ThrowsNamingIt()
        {
            WriteNotADocument();

            var thrown = Assert.ThrowsException<PortalException>(() =>
                AssemblyHooks.SharedPortal.ImportFromDocuments(Request(string.Empty), NotADocumentName, "Override"));

            StringAssert.Contains(thrown.Message, NotADocumentName);
        }

        private DocumentImportRequest Request(string groupPath)
        {
            return new DocumentImportRequest(Software, groupPath, _importDirectory, Path.Combine(_directory, "backup"));
        }

        private void WriteValidDocument()
        {
            var table = AssemblyHooks.SharedPortal.GetTagTables(Software).FirstOrDefault(one => one.IsDefault);
            Assert.IsNotNull(table, "The PLC program has no default tag table");
            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition($"{table.Path}/{Test33DocumentExport.TagName}", "Bool", "%M90.0"), _directory);

            var source = Test33DocumentExport.BrokenBlock.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(Path.Combine(_importDirectory, $"{Test33DocumentExport.BrokenName}.s7dcl"), source, new UTF8Encoding(true));
        }

        private void WriteNotADocument()
        {
            File.WriteAllText(Path.Combine(_importDirectory, $"{NotADocumentName}.s7dcl"), "This is not a SIMATIC SD document.", new UTF8Encoding(true));
        }
    }
}
