using System;
using System.IO;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Spec;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The generated cell compiles in TIA Portal.
    /// </summary>
    /// <remarks>
    /// This is the only test in phase 2 that can say the patterns are correct. An expander test proves
    /// the text came out as intended; it says nothing about whether the SCL is valid, and SCL that
    /// looks right and does not compile is the normal outcome of writing PLC code from memory.
    ///
    /// So it writes the real templates from <c>spec/</c> into the fixture project and compiles them,
    /// through the same guarded tools an agent would use. Phase 1 built that loop; this is the first
    /// thing to actually need it.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test19CellPattern
    {
        private static string _repositoryRoot = string.Empty;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _repositoryRoot = FindRepositoryRoot();
        }

        [TestInitialize]
        public void TestInit()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void TwoStationDemo_WrittenAndCompiled_ReportsNoErrors()
        {
            // Two stations contain the whole of the coordination, which is why the roadmap says two
            // that work are worth more than four in a diagram.
            AssertCellCompiles("two-station-demo.json", "FB_TwoStationDemo");
        }

        [TestMethod]
        public void FourStationCell_WrittenAndCompiled_ReportsNoErrors()
        {
            // The same patterns with two more entries in the JSON. If this needed a code change, the
            // pattern would not be one.
            AssertCellCompiles("four-station-cell.json", "FB_FourStationCell");
        }

        [TestMethod]
        public void ExpandCellScl_TheTwoStationDemo_ReturnsBothBlocksAndWritesNothing()
        {
            // The tool an agent actually calls. It touches no project, so there is nothing to undo
            // if the generated code is wrong - which is why it returns the source instead of writing
            // it, and why WriteScl stays the only thing that changes a project.
            var response = McpServer.ExpandCellScl(
                Path.Combine(_repositoryRoot, "spec", "cells", "two-station-demo.json"),
                Path.Combine(_repositoryRoot, "spec", "patterns"));

            Assert.AreEqual("TwoStationDemo", response.CellName);
            Assert.AreEqual(2, response.StationNames.Count);
            StringAssert.Contains(response.Scl, "FUNCTION_BLOCK \"FB_Station\"", StringComparison.Ordinal);
            StringAssert.Contains(response.Scl, "FUNCTION_BLOCK \"FB_TwoStationDemo\"", StringComparison.Ordinal);

            // Order, not presence: the coordinator declares instances of the station, so a source
            // with them the other way round does not compile.
            Assert.IsTrue(
                response.Scl.IndexOf("FB_Station\"", StringComparison.Ordinal)
                < response.Scl.IndexOf("FB_TwoStationDemo\"", StringComparison.Ordinal),
                "the station pattern must come before the coordinator that instantiates it");
        }

        [TestMethod]
        public void ExpandCellScl_WithTheEntryPoint_ReturnsTheInstanceDataBlockAndMainLast()
        {
            // Opt-in, because it replaces the project's Main. What it adds is the only thing that
            // makes the cell run rather than merely exist: an instance of the coordinator and an OB
            // that calls it every scan.
            var response = McpServer.ExpandCellScl(
                Path.Combine(_repositoryRoot, "spec", "cells", "two-station-demo.json"),
                Path.Combine(_repositoryRoot, "spec", "patterns"),
                includeEntryPoint: true);

            StringAssert.Contains(response.Scl, "DATA_BLOCK \"DB_TwoStationDemo\"", StringComparison.Ordinal);
            StringAssert.Contains(response.Scl, "ORGANIZATION_BLOCK \"Main\"", StringComparison.Ordinal);

            // Order again, and for the same reason as the station and the coordinator: the data
            // block is an instance of FB_TwoStationDemo, so declaring it first does not compile.
            Assert.IsTrue(
                response.Scl.IndexOf("FUNCTION_BLOCK \"FB_TwoStationDemo\"", StringComparison.Ordinal)
                < response.Scl.IndexOf("DATA_BLOCK \"DB_TwoStationDemo\"", StringComparison.Ordinal),
                "the coordinator must come before the data block that instantiates it");
        }

        [TestMethod]
        public void ExpandCellScl_WithAPatternDirectoryThatIsNotThere_IsBadInputRatherThanAFailure()
        {
            var exception = Assert.ThrowsException<global::ModelContextProtocol.McpException>(
                () => McpServer.ExpandCellScl(
                    Path.Combine(_repositoryRoot, "spec", "cells", "two-station-demo.json"),
                    Path.Combine(_repositoryRoot, "no-such-patterns")));

            Assert.AreEqual(global::ModelContextProtocol.McpErrorCode.InvalidParams, exception.ErrorCode);
            StringAssert.Contains(exception.Message, "spec/patterns/", StringComparison.Ordinal);
        }

        private static void AssertCellCompiles(string cellFile, string coordinatorBlock)
        {
            var scl = ExpandCell(cellFile);

            var written = McpServer.WriteScl(Settings.Project1PlcSoftwarePath0, scl);

            // Both blocks by name, not a count. A compile that reports no errors would also report no
            // errors if the coordinator had never been generated, and that is exactly the failure this
            // test exists to catch.
            CollectionAssert.Contains(
                written.GeneratedBlocks.ToList(),
                "FB_Station",
                $"the station block was not generated: {written.Message}");

            CollectionAssert.Contains(
                written.GeneratedBlocks.ToList(),
                coordinatorBlock,
                $"the coordinator block was not generated: {written.Message}");

            var compiled = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            Assert.AreEqual(
                0,
                compiled.ErrorCount,
                "the generated cell does not compile: " + string.Join(" | ", compiled.Messages));
        }

        private static string ExpandCell(string cellFile)
        {
            var expander = new SclTemplateExpander();
            var cell = CellSpecificationFile.Load(Path.Combine(_repositoryRoot, "spec", "cells", cellFile));

            var station = expander.Expand(ReadPattern("station.scl.tmpl"), cell);
            var coordinator = expander.Expand(ReadPattern("coordinator.scl.tmpl"), cell);

            // One source, both blocks: WriteScl generates every block a source declares, and the
            // coordinator cannot compile before the station type it instantiates exists.
            return station + Environment.NewLine + coordinator;
        }

        private static string ReadPattern(string pattern)
        {
            return File.ReadAllText(Path.Combine(_repositoryRoot, "spec", "patterns", pattern));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "spec")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "could not find the repository root, so spec/ cannot be read");

            return directory!.FullName;
        }
    }
}
