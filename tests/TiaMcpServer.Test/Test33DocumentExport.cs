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
    /// Export as SIMATIC SD documents of a block that does not compile.
    /// </summary>
    /// <remarks>
    /// SimaticML export refuses an inconsistent block; SD export does not (measured on TIA Portal
    /// V20, 2026-09-24), and it is the only way to read the source of a broken block — which is when
    /// a person most needs to read it. The bulk export used to skip such blocks by a check of its own;
    /// these tests hold that it exports them and still says they are broken.
    ///
    /// The broken block is built here, the way it was met in practice: a LAD function whose TON names
    /// an instance DB that does not exist. It imports and does not compile ("Missing instance DB").
    /// The test asserts that it does not compile before relying on it, so a fixture that stops being
    /// broken fails loudly instead of letting the tests pass for the wrong reason.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test33DocumentExport
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string BrokenName = "FC_BrokenByTest";
        private const string TagName = "TiaMcpBrokenDone";

        private const string BrokenBlock = @"{
    S7_Optimized := ""TRUE"";
    S7_PreferredLanguage := ""LAD"";
    S7_Version := ""0.1""
}
FUNCTION ""FC_BrokenByTest"" : Void
    { S7_Language := ""LAD"" }
    NETWORK
        RUNG wire#powerrail
            Contact( ""TiaMcpBrokenDone"" )
            ""NoSuchTimer_DB"".TON(
                pt := T#1S,
                et =>
            )
            Coil( ""TiaMcpBrokenDone"" )
        END_RUNG
    END_NETWORK
END_FUNCTION
";

        private string _directory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _directory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
            McpServer.Portal = AssemblyHooks.SharedPortal;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void ExportBlocksAsDocuments_ABlockThatDoesNotCompile_WritesItsDocument()
        {
            ExportBroken(out var exportDirectory);

            var document = Path.Combine(exportDirectory, $"{BrokenName}.s7dcl");

            Assert.IsTrue(File.Exists(document), $"No document was written for the broken block in {exportDirectory}");
            StringAssert.Contains(File.ReadAllText(document), "NoSuchTimer_DB", "The document is not the source of the broken block");
        }

        /// <remarks>
        /// The other half of the rule in CLAUDE.md: exported, and still reported as broken, so the
        /// document of a block that does not compile is never mistaken for a working one.
        /// </remarks>
        [TestMethod]
        public void ExportBlocksAsDocuments_ABlockThatDoesNotCompile_IsReportedAsInconsistent()
        {
            var exported = ExportBroken(out _);

            var broken = exported.SingleOrDefault(block => block.Name == BrokenName);

            Assert.IsNotNull(broken, "The broken block is missing from what was exported");
            Assert.IsFalse(broken.IsConsistent, "A block that does not compile was reported as consistent");
        }

        /// <remarks>
        /// A block whose document cannot be written must be named in the response with its reason,
        /// not left for the caller to notice by counting. The document is held open with no sharing,
        /// so neither removing the stale one nor writing the new one can succeed. No server and no
        /// request context: what is under test is the response, not the progress notifications.
        /// </remarks>
        [TestMethod]
        public async Task ExportBlocksAsDocuments_ADocumentThatCannotBeWritten_IsNamedInFailed()
        {
            ImportBrokenBlock();
            var exportDirectory = Path.Combine(_directory, "export");
            Directory.CreateDirectory(exportDirectory);

            ResponseExportBlocksAsDocuments response;
            using (File.Open(Path.Combine(exportDirectory, $"{BrokenName}.s7dcl"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                response = await McpServer.ExportBlocksAsDocuments(null!, null!, Software, exportDirectory, BrokenName);
            }

            var failures = response.Failed?.ToList() ?? new List<string>();
            Assert.IsTrue(failures.Any(line => line.StartsWith($"{BrokenName}:")), $"The block whose document is locked is not named in Failed: {response.Message}");
        }

        private IReadOnlyList<BlockDescription> ExportBroken(out string exportDirectory)
        {
            ImportBrokenBlock();

            exportDirectory = Path.Combine(_directory, "export");
            var report = AssemblyHooks.SharedPortal.ExportBlocksAsDocuments(Software, exportDirectory, BrokenName);

            Assert.IsNotNull(report, "The document export returned nothing");
            return report.Exported;
        }

        private void ImportBrokenBlock()
        {
            var table = AssemblyHooks.SharedPortal.GetTagTables(Software).FirstOrDefault(one => one.IsDefault);
            Assert.IsNotNull(table, "The PLC program has no default tag table");
            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition($"{table.Path}/{TagName}", "Bool", "%M90.0"), _directory);

            var importDirectory = Path.Combine(_directory, "import");
            Directory.CreateDirectory(importDirectory);
            File.WriteAllText(Path.Combine(importDirectory, $"{BrokenName}.s7dcl"), BrokenBlock.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(true));

            var imported = AssemblyHooks.SharedPortal.ImportFromDocuments(Software, string.Empty, importDirectory, BrokenName, "Override");
            Assert.IsTrue(imported, "The broken block could not be imported, so there is nothing to export");

            var compiled = AssemblyHooks.SharedPortal.CompileSoftware(Software);
            Assert.IsFalse(compiled.IsSuccessful, "The fixture compiled, so it no longer tests a broken block");
        }
    }
}
