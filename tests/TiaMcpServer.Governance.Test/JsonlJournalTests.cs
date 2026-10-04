using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.History;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class JsonlJournalTests
    {
        private string _directory = string.Empty;

        [TestInitialize]
        public void TestInit()
        {
            _directory = Path.Combine(Path.GetTempPath(), "JsonlJournalTests", Guid.NewGuid().ToString("N"));
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
        public void Append_TwoRecords_WritesOneLineEach()
        {
            var path = Path.Combine(_directory, "nested", "compilations.jsonl");
            var journal = new JsonlJournal<CompilationRecord>(path);

            journal.Append(ACompilation(errorCount: 0));
            journal.Append(ACompilation(errorCount: 2));

            var lines = File.ReadAllLines(path).Where(line => line.Length > 0).ToArray();

            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual(2, JsonDocument.Parse(lines[1]).RootElement.GetProperty("errorCount").GetInt32());
        }

        [TestMethod]
        public void Append_AnEnumeration_WritesItsName()
        {
            var path = Path.Combine(_directory, "compilations.jsonl");

            new JsonlJournal<CompilationRecord>(path).Append(ACompilation(errorCount: 1));

            var root = JsonDocument.Parse(File.ReadAllText(path)).RootElement;

            Assert.AreEqual("Error", root.GetProperty("severity").GetString());
            Assert.AreEqual("Error", root.GetProperty("messages")[0].GetProperty("severity").GetString());
        }

        /// <remarks>
        /// A byte order mark on every append would land in the middle of the file, and the line
        /// after it would stop being JSON for every reader but the most forgiving.
        /// </remarks>
        [TestMethod]
        public void Append_TwoRecords_WritesNoByteOrderMark()
        {
            var path = Path.Combine(_directory, "compilations.jsonl");
            var journal = new JsonlJournal<CompilationRecord>(path);

            journal.Append(ACompilation(errorCount: 0));
            journal.Append(ACompilation(errorCount: 0));

            var bytes = File.ReadAllBytes(path);

            Assert.AreNotEqual(0xEF, bytes[0]);
            Assert.AreEqual(-1, IndexOfByteOrderMark(bytes));
        }

        [TestMethod]
        public void Append_APathThatIsADirectory_ThrowsPortalException()
        {
            Directory.CreateDirectory(_directory);

            var journal = new JsonlJournal<CompilationRecord>(_directory);

            Assert.ThrowsException<PortalException>(() => journal.Append(ACompilation(errorCount: 0)));
        }

        internal static CompilationRecord ACompilation(int errorCount)
        {
            var severity = errorCount > 0 ? CompilationSeverity.Error : CompilationSeverity.Success;
            var messages = errorCount > 0
                ? new[] { new CompilationMessage(CompilationSeverity.Error, "Main", "Tag not defined") }
                : Array.Empty<CompilationMessage>();

            return new CompilationRecord(
                new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
                @"C:\Projects\Cell\Cell.ap20",
                "PLC_0",
                new CompilationReport(severity, errorCount, 0, messages));
        }

        private static int IndexOfByteOrderMark(byte[] bytes)
        {
            for (var index = 0; index + 2 < bytes.Length; index++)
            {
                if (bytes[index] == 0xEF && bytes[index + 1] == 0xBB && bytes[index + 2] == 0xBF)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
