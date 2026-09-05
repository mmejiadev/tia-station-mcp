using System.IO;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <remarks>
    /// Block and type paths are taken from a snapshot of TestProject1 that was actually inspected,
    /// not from what the upstream fixture assumed. Export directories are created per test under
    /// <see cref="AssemblyHooks.WorkingRoot"/>, replacing the absolute <c>D:\Temp\TIA-Portal\...</c>
    /// paths these tests used to depend on.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test4Software
    {
        private const string BlockPath = "1_Tests/FC_Block_1";
        private const string BlockGroupPath = "1_Tests";
        private const string TypePath = "Common/CarrierRegister/ML_SubstratState";
        private const string TypeGroupPath = "Common/CarrierRegister";

        private string _exportDirectory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _exportDirectory = AssemblyHooks.CreateTestDirectory();
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void CompileSoftware_PlcSoftware_ReportsSuccess()
        {
            // Only one software is compiled. Compiling all eight would add minutes to every run
            // for coverage of the same code path.
            var report = AssemblyHooks.SharedPortal.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            Assert.IsTrue(
                report.IsSuccessful,
                $"Compile reported {report.ErrorCount} error(s):\n{string.Join("\n", report.Errors)}");
        }

        /// <remarks>
        /// The PLC software is what every block and type tool is addressed through, and the tool
        /// that describes it is now the only thing that touches the <c>PlcSoftware</c> object.
        /// </remarks>
        [TestMethod]
        public void GetPlcSoftware_ExistingPath_DescribesIt()
        {
            var software = AssemblyHooks.SharedPortal.GetPlcSoftware(Settings.Project1PlcSoftwarePath0);

            Assert.IsNotNull(software, $"No PLC software at '{Settings.Project1PlcSoftwarePath0}'");
            Assert.IsFalse(string.IsNullOrEmpty(software.Name), "The software was described with no name");
            Assert.IsTrue(software.Attributes.Count > 0, "No attribute was read");
        }

        [TestMethod]
        public void GetPlcSoftware_UnknownPath_ReturnsNull()
        {
            var software = AssemblyHooks.SharedPortal.GetPlcSoftware("NoSuchDevice/NoSuchPlc");

            Assert.IsNull(software, "An unknown path returned a software");
        }

        [TestMethod]
        public void CompileSoftware_UnknownSoftwarePath_ThrowsNotFound()
        {
            var exception = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.CompileSoftware("NoSuchDevice/NoSuchPlc"));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void GetBlock_ExistingPath_ReturnsBlock()
        {
            var block = AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, BlockPath);

            Assert.IsNotNull(block, $"No block found at '{BlockPath}'");
            Assert.AreEqual("FC_Block_1", block.Name);
        }

        /// <remarks>
        /// The description is what leaves the portal layer, so the path has to travel inside it.
        /// Before the split the MCP layer worked the path out by walking a live block back up its
        /// parents, which is exactly the kind of call this DTO exists to stop.
        /// </remarks>
        [TestMethod]
        public void GetBlock_ExistingPath_DescriptionCarriesItsFullPath()
        {
            var block = AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, BlockPath);

            Assert.IsNotNull(block, $"No block found at '{BlockPath}'");
            Assert.AreEqual(BlockPath, block.Path);
        }

        /// <remarks>
        /// A description is read once and detached: everything a caller needs must already be in
        /// it, because the block it came from is gone by the time anyone reads this.
        /// </remarks>
        [TestMethod]
        public void GetBlock_ExistingPath_DescriptionIsComplete()
        {
            var block = AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, BlockPath);

            Assert.IsNotNull(block);
            Assert.AreEqual("FC", block.TypeName);
            Assert.IsFalse(string.IsNullOrEmpty(block.ProgrammingLanguage), "The language was not read");
            Assert.IsTrue(block.Attributes.Count > 0, "No attribute was read");
        }

        [TestMethod]
        public void GetType_ExistingPath_ReturnsType()
        {
            var type = AssemblyHooks.SharedPortal.GetType(Settings.Project1PlcSoftwarePath0, TypePath);

            Assert.IsNotNull(type, $"No type found at '{TypePath}'");
            Assert.AreEqual("ML_SubstratState", type.Name);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("^F.+")]
        public void GetBlocks_Regex_ReturnsMatchingBlocks(string regexName)
        {
            var blocks = AssemblyHooks.SharedPortal.GetBlocks(Settings.Project1PlcSoftwarePath0, regexName);

            Assert.IsNotNull(blocks);
            Assert.IsTrue(blocks.Count > 0, $"No block matched '{regexName}'");
        }

        /// <remarks>
        /// A path is what every other tool takes as input, so a listing that does not give one back
        /// forces the caller to guess at the grouping.
        /// </remarks>
        [TestMethod]
        public void GetBlocks_NoRegex_EveryDescriptionHasAPath()
        {
            var blocks = AssemblyHooks.SharedPortal.GetBlocks(Settings.Project1PlcSoftwarePath0, string.Empty);

            Assert.IsTrue(blocks.Count > 0, "The program has blocks but none were returned");
            Assert.IsFalse(
                blocks.Any(block => string.IsNullOrEmpty(block.Path)),
                "Some block was described without its path");
        }

        /// <remarks>
        /// The hierarchy is the shape of the program, and it is now read in the portal layer rather
        /// than walked lazily from above, one Openness call per group the caller expanded.
        /// </remarks>
        [TestMethod]
        public void GetBlockHierarchy_PlcSoftware_ContainsTheTestGroup()
        {
            var root = AssemblyHooks.SharedPortal.GetBlockHierarchy(Settings.Project1PlcSoftwarePath0);

            Assert.IsNotNull(root, "The block root group could not be read");
            Assert.IsTrue(
                root.Groups.Any(group => group.Name == BlockGroupPath),
                $"'{BlockGroupPath}' is not among the groups of the root");
        }

        /// <remarks>
        /// This was a real defect, found while extracting ProjectPath and not by anything failing.
        /// An empty block path became "the root group", an empty name became a filter that matches
        /// everything, and GetBlock returned whichever block came first -- a different block on a
        /// different project, reported as though it were the one asked for. Every write tool takes
        /// a path like this one.
        /// </remarks>
        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("/")]
        public void GetBlock_APathThatNamesNoBlock_RefusesInsteadOfReturningOne(string blockPath)
        {
            var failure = Assert.ThrowsException<PortalException>(
                () => AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, blockPath));

            Assert.AreEqual(PortalErrorCode.InvalidParams, failure.Code);
        }

        /// <remarks>
        /// The forgiving half of the same rule, and it has to hold through the real lookup rather
        /// than only in the unit tests: a doubled or trailing slash is a typing artefact.
        /// </remarks>
        [TestMethod]
        [DataRow("1_Tests//FC_Block_1")]
        [DataRow("/1_Tests/FC_Block_1")]
        [DataRow(" 1_Tests / FC_Block_1 ")]
        public void GetBlock_ASlashOutOfPlace_StillFindsTheBlock(string blockPath)
        {
            var block = AssemblyHooks.SharedPortal.GetBlock(Settings.Project1PlcSoftwarePath0, blockPath);

            Assert.IsNotNull(block, $"'{blockPath}' found no block");
            Assert.AreEqual("FC_Block_1", block.Name);
        }

        [TestMethod]
        public void GetBlocks_RegexMatchingNothing_ReturnsEmptyList()
        {
            var blocks = AssemblyHooks.SharedPortal.GetBlocks(Settings.Project1PlcSoftwarePath0, "^NoSuchBlockName$");

            Assert.IsNotNull(blocks, "An empty result must be a list, not null");
            Assert.AreEqual(0, blocks.Count);
        }

        [TestMethod]
        public void GetTypes_NoRegex_ReturnsTypes()
        {
            var types = AssemblyHooks.SharedPortal.GetTypes(Settings.Project1PlcSoftwarePath0, string.Empty);

            Assert.IsNotNull(types);
            Assert.IsTrue(types.Count > 0, "The project has UDTs but none were returned");
        }

        /// <remarks>
        /// The path has to be the one a caller writes, which for this type is two groups deep. The
        /// walker the description uses stops below the root system group; the one the preserve-path
        /// export uses does not, and that difference is deliberate.
        /// </remarks>
        [TestMethod]
        public void GetType_ExistingPath_DescriptionCarriesItsFullPath()
        {
            var type = AssemblyHooks.SharedPortal.GetType(Settings.Project1PlcSoftwarePath0, TypePath);

            Assert.IsNotNull(type, $"No type found at '{TypePath}'");
            Assert.AreEqual(TypePath, type.Path);
        }

        /// <remarks>
        /// A description is read once and detached, so everything a caller needs must already be in
        /// it: the type it came from is gone by the time anyone reads this.
        /// </remarks>
        [TestMethod]
        public void GetType_ExistingPath_DescriptionIsComplete()
        {
            var type = AssemblyHooks.SharedPortal.GetType(Settings.Project1PlcSoftwarePath0, TypePath);

            Assert.IsNotNull(type);
            Assert.AreEqual("ML_SubstratState", type.Name);
            Assert.IsFalse(string.IsNullOrEmpty(type.TypeName), "The Openness type name was not read");
            Assert.IsTrue(type.Attributes.Count > 0, "No attribute was read");
        }

        [TestMethod]
        public void GetTypes_NoRegex_EveryDescriptionHasAPath()
        {
            var types = AssemblyHooks.SharedPortal.GetTypes(Settings.Project1PlcSoftwarePath0, string.Empty);

            Assert.IsTrue(types.Count > 0, "The project has UDTs but none were returned");
            Assert.IsFalse(
                types.Any(type => string.IsNullOrEmpty(type.Path)),
                "Some type was described without its path");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ExportBlock_ConsistentBlock_WritesFile(bool preservePath)
        {
            AssemblyHooks.SharedPortal.ExportBlock(Settings.Project1PlcSoftwarePath0, BlockPath, _exportDirectory, preservePath);

            Assert.IsTrue(
                Directory.EnumerateFiles(_exportDirectory, "FC_Block_1.xml", SearchOption.AllDirectories).Any(),
                $"No exported file found under {_exportDirectory}");
        }

        [TestMethod]
        public void ExportType_ConsistentType_WritesFile()
        {
            AssemblyHooks.SharedPortal.ExportType(Settings.Project1PlcSoftwarePath0, TypePath, _exportDirectory, preservePath: false);

            Assert.IsTrue(
                Directory.EnumerateFiles(_exportDirectory, "ML_SubstratState.xml", SearchOption.AllDirectories).Any(),
                $"No exported type found under {_exportDirectory}");
        }

        [TestMethod]
        public void ExportBlocks_AllBlocks_WritesFiles()
        {
            var exported = AssemblyHooks.SharedPortal.ExportBlocks(Settings.Project1PlcSoftwarePath0, _exportDirectory, string.Empty, preservePath: true);

            Assert.IsNotNull(exported);
            Assert.IsTrue(
                Directory.EnumerateFiles(_exportDirectory, "*.xml", SearchOption.AllDirectories).Any(),
                "ExportBlocks wrote no files");
        }

        [TestMethod]
        public void ExportTypes_AllTypes_WritesFiles()
        {
            var exported = AssemblyHooks.SharedPortal.ExportTypes(Settings.Project1PlcSoftwarePath0, _exportDirectory, string.Empty, preservePath: true);

            Assert.IsNotNull(exported);
            Assert.IsTrue(
                Directory.EnumerateFiles(_exportDirectory, "*.xml", SearchOption.AllDirectories).Any(),
                "ExportTypes wrote no files");
        }

        [TestMethod]
        public void ImportBlock_PreviouslyExportedBlock_Succeeds()
        {
            // Round trip rather than a fixture file: exporting first means the import always has a
            // document the current TIA version accepts.
            AssemblyHooks.SharedPortal.ExportBlock(Settings.Project1PlcSoftwarePath0, BlockPath, _exportDirectory, preservePath: false);
            var exportedFile = Directory.EnumerateFiles(_exportDirectory, "FC_Block_1.xml", SearchOption.AllDirectories).Single();

            var result = AssemblyHooks.SharedPortal.ImportBlock(Settings.Project1PlcSoftwarePath0, BlockGroupPath, exportedFile);

            Assert.IsTrue(result, $"Failed to import {exportedFile}");
        }

        [TestMethod]
        public void ImportType_PreviouslyExportedType_Succeeds()
        {
            AssemblyHooks.SharedPortal.ExportType(Settings.Project1PlcSoftwarePath0, TypePath, _exportDirectory, preservePath: false);
            var exportedFile = Directory.EnumerateFiles(_exportDirectory, "ML_SubstratState.xml", SearchOption.AllDirectories).Single();

            var result = AssemblyHooks.SharedPortal.ImportType(Settings.Project1PlcSoftwarePath0, TypeGroupPath, exportedFile);

            Assert.IsTrue(result, $"Failed to import {exportedFile}");
        }
    }
}
