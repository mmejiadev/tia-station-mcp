using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The backup every program write takes first, and what happens when it cannot be taken.
    /// </summary>
    /// <remarks>
    /// The backup used to be the bulk SimaticML export, which skips a block that does not compile
    /// and only logs what it could not write. In the generate, compile, fix loop the block about to
    /// be overwritten is usually the one that does not compile — so the backup left out the one copy
    /// that mattered, and the write went ahead.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test36ProgramBackup
    {
        private const string Software = Settings.Project1PlcSoftwarePath0;
        private const string GeneratedName = "FC_BackedUpByTest";

        private const string ValidScl = @"FUNCTION ""FC_BackedUpByTest"" : Void
VERSION : 0.1
BEGIN
    ; // deliberately empty body
END_FUNCTION
";

        private string _directory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _directory = AssemblyHooks.CreateTestDirectory();
            Directory.CreateDirectory(_directory);
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void WriteScl_AProgramWithABlockThatDoesNotCompile_BacksItUpAsADocument()
        {
            ImportBrokenBlock();
            var backupDirectory = Path.Combine(_directory, "backup");

            AssemblyHooks.SharedPortal.WriteScl(Software, ValidScl, backupDirectory);

            var saved = Directory.EnumerateFiles(backupDirectory, $"{Test33DocumentExport.BrokenName}.s7dcl", SearchOption.AllDirectories);
            Assert.IsTrue(saved.Any(), $"The block that does not compile is missing from the backup in {backupDirectory}");
        }

        /// <remarks>
        /// A file where the backup directory should be: nothing can be saved below it, so the write
        /// must be refused and the block must not exist afterwards.
        /// </remarks>
        [TestMethod]
        public void WriteScl_BackupThatCannotBeWritten_WritesNothing()
        {
            var notADirectory = Path.Combine(_directory, "backup.txt");
            File.WriteAllText(notADirectory, "A file, not a directory.");

            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.WriteScl(Software, ValidScl, notADirectory));

            Assert.AreEqual(PortalErrorCode.WriteFailed, failure.Code, failure.Message);
            Assert.IsNull(AssemblyHooks.SharedPortal.GetBlock(Software, GeneratedName), "The block was written although the backup failed");
        }

        [TestMethod]
        public void ImportBlock_ExistingBlock_BackupHoldsThePreviousVersion()
        {
            var exportDirectory = Path.Combine(_directory, "export");
            AssemblyHooks.SharedPortal.ExportBlock(Software, "1_Tests/FC_Block_1", exportDirectory);
            var exportedFile = Directory.EnumerateFiles(exportDirectory, "FC_Block_1.xml", SearchOption.AllDirectories).Single();
            var backupDirectory = Path.Combine(_directory, "backup");

            AssemblyHooks.SharedPortal.ImportBlock(Software, "1_Tests", exportedFile, backupDirectory);

            var saved = Directory.EnumerateFiles(backupDirectory, "FC_Block_1.xml", SearchOption.AllDirectories);
            Assert.IsTrue(saved.Any(), $"The block that was replaced is missing from the backup in {backupDirectory}");
        }

        /// <remarks>
        /// The same broken block Test33DocumentExport measures TIA Portal V20 to accept: a LAD block
        /// that imports from a document and does not compile.
        /// </remarks>
        private void ImportBrokenBlock()
        {
            var table = AssemblyHooks.SharedPortal.GetTagTables(Software).FirstOrDefault(one => one.IsDefault);
            Assert.IsNotNull(table, "The PLC program has no default tag table");
            AssemblyHooks.SharedPortal.CreateTag(Software, new TagDefinition($"{table.Path}/{Test33DocumentExport.TagName}", "Bool", "%M90.0"), _directory);

            var importDirectory = Path.Combine(_directory, "import");
            Directory.CreateDirectory(importDirectory);
            var source = Test33DocumentExport.BrokenBlock.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(Path.Combine(importDirectory, $"{Test33DocumentExport.BrokenName}.s7dcl"), source, new UTF8Encoding(true));

            var request = new DocumentImportRequest(Software, string.Empty, importDirectory, Path.Combine(_directory, "import-backup"));
            AssemblyHooks.SharedPortal.ImportFromDocuments(request, Test33DocumentExport.BrokenName, "Override");

            var compiled = AssemblyHooks.SharedPortal.CompileSoftware(Software);
            Assert.IsFalse(compiled.IsSuccessful, "The fixture compiled, so it no longer tests a broken block");
        }
    }
}
