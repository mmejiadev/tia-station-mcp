using System;
using System.IO;
using System.Linq;
using TiaMcpServer.History;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <summary>
    /// The journals are written in C# and read in TypeScript, and these files are the contract.
    /// </summary>
    /// <remarks>
    /// The golden files in <c>platform/test/assets/</c> are what the platform's importer tests import.
    /// These tests assert the server writes them byte for byte, so a change to either side — a
    /// renamed property, an enumeration written as a number, a different escaping — fails here or
    /// there instead of in an import that quietly reads blanks. The same arrangement as the audit
    /// chain's golden files, for the same reason.
    /// </remarks>
    [TestClass]
    public sealed class JournalContractTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        private string _directory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _directory = Path.Combine(Path.GetTempPath(), "JournalContractTests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [TestMethod]
        public void CompilationJournal_AsTheServerWritesIt_IsTheGoldenFileThePlatformReads()
        {
            var path = Path.Combine(_directory, "compilations.jsonl");
            var journal = new JsonlJournal<CompilationRecord>(path);

            journal.Append(JsonlJournalTests.ACompilation(errorCount: 1));
            journal.Append(JsonlJournalTests.ACompilation(errorCount: 0));

            CollectionAssert.AreEqual(LinesOf(Golden("compilations-golden.jsonl")), LinesOf(path));
        }

        [TestMethod]
        public void ProjectJournal_AsTheServerWritesIt_IsTheGoldenFileThePlatformReads()
        {
            var path = Path.Combine(_directory, "projects.jsonl");
            var project = new ProjectSummary(
                @"C:\Projects\Cell\Cell.ap20",
                "Cell",
                "mamem",
                new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 2, 17, 30, 0, TimeSpan.Zero),
                "mamem");

            new JsonlJournal<ProjectRecord>(path).Append(new ProjectRecord(Now, ProjectEvent.Opened, project));

            CollectionAssert.AreEqual(LinesOf(Golden("projects-golden.jsonl")), LinesOf(path));
        }

        /// <remarks>
        /// Line endings are not part of the contract: Git checks the golden files out with CRLF and
        /// the server writes the platform's newline, and the importer reads both.
        /// </remarks>
        private static string[] LinesOf(string path)
        {
            return File.ReadAllLines(path).Where(line => line.Trim().Length > 0).ToArray();
        }

        private static string Golden(string name)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TiaMcpServer.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                Assert.Inconclusive("Not running from inside a checkout, so the golden files cannot be found.");
            }

            return Path.Combine(directory!.FullName, "platform", "test", "assets", name);
        }
    }
}
