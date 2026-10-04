using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The history the web platform imports: a compilation and an opened project, recorded by the
    /// tools rather than by the portal.
    /// </summary>
    /// <remarks>
    /// Through <c>McpServer</c>, because the recording lives in the tools: a test of the portal would
    /// pass with the tools never writing a line. The journals are this run's, under the working root
    /// <see cref="AssemblyHooks"/> builds the services with.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test38History
    {
        [TestInitialize]
        public void TestInit()
        {
            McpServer.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            McpServer.CloseProject();
        }

        [TestMethod]
        public void CompileSoftware_ThroughTheTool_RecordsWhatTheCompilerSaid()
        {
            var response = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            using var recorded = LastLineOf(AssemblyHooks.CompilationsPath);
            var root = recorded.RootElement;

            Assert.AreEqual(Settings.Project1PlcSoftwarePath0, root.GetProperty("softwarePath").GetString());
            Assert.AreEqual(response.ErrorCount, root.GetProperty("errorCount").GetInt32());
            Assert.AreEqual(response.WarningCount, root.GetProperty("warningCount").GetInt32());
            StringAssert.EndsWith(root.GetProperty("projectPath").GetString(), ".ap20", StringComparison.OrdinalIgnoreCase);
        }

        /// <remarks>
        /// A word, not a number: the importer reads "Error" and keeps reading it when the
        /// enumeration grows.
        /// </remarks>
        [TestMethod]
        public void CompileSoftware_ThroughTheTool_RecordsTheSeverityAsAWord()
        {
            McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0);

            using var recorded = LastLineOf(AssemblyHooks.CompilationsPath);

            Assert.AreEqual(JsonValueKind.String, recorded.RootElement.GetProperty("severity").ValueKind);
        }

        [TestMethod]
        public void OpenProject_ThroughTheTool_RecordsTheProjectAndWhoMadeIt()
        {
            using var recorded = LastLineOf(AssemblyHooks.ProjectsPath);
            var root = recorded.RootElement;

            Assert.AreEqual("Opened", root.GetProperty("event").GetString());
            Assert.AreEqual(Path.GetFullPath(AssemblyHooks.ProjectPath), Path.GetFullPath(root.GetProperty("path").GetString()!));
            Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("author").GetString()), $"No author recorded: {root}");
        }

        private static JsonDocument LastLineOf(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Nothing was recorded at {path}");

            var last = File.ReadAllLines(path).Last(line => line.Trim().Length > 0);

            return JsonDocument.Parse(last);
        }
    }
}
