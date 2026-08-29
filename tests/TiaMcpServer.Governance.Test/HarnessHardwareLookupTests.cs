using System;
using System.IO;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The one test in this project that runs the real thing: Node, the harness lookup and an index
    /// built from real manuals. Everything else here is a unit test against a double, and a double
    /// cannot notice that the arguments were quoted wrongly or that the JSON field is called
    /// something else.
    ///
    /// It reports **inconclusive**, never failure, on a machine without Node or without a built
    /// index. The governance suite has to pass on any machine — that is why it is a separate project
    /// — and a test that fails for a missing optional dependency would make the suite say "broken"
    /// when the honest answer is "not checkable here". On the machine that has both, it runs.
    /// </remarks>
    [TestClass]
    public class HarnessHardwareLookupTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        [TestMethod]
        public void Describe_AQuestionTheCorpusCovers_CitesARealPage()
        {
            var lookup = LookupOrInconclusive();

            var context = lookup.Describe("UR5e safety I/O configurable inputs");

            Assert.AreEqual(HardwareContextOutcome.Cited, context.Outcome, context.Summarise());
            Assert.IsTrue(context.Citations.Count > 0);
            Assert.IsTrue(context.Citations[0].Page > 0, "a citation without a page cannot be checked");
            Assert.IsFalse(string.IsNullOrWhiteSpace(context.Citations[0].Excerpt));
            Assert.IsFalse(string.IsNullOrWhiteSpace(context.Citations[0].Document.Title));
        }

        [TestMethod]
        public void Describe_AQuestionTheCorpusDoesNotCover_AbstainsRatherThanReaching()
        {
            // The cardinal rule, end to end and through the process boundary: a ranking always has a
            // best row, and the answer here has to be silence rather than whichever excerpt scored
            // least badly.
            var lookup = LookupOrInconclusive();

            var context = lookup.Describe("what is the capital of France");

            Assert.AreEqual(HardwareContextOutcome.NotFound, context.Outcome, context.Summarise());
            Assert.AreEqual(0, context.Citations.Count);
        }

        [TestMethod]
        public void Describe_NoIndexOnThisMachine_ReportsItRatherThanThrowing()
        {
            // No Node needed: the missing file is noticed before anything is started, which is what
            // keeps an unconfigured machine from paying for a process on every single write.
            var lookup = new HarnessHardwareLookup(
                Path.Combine(RepositoryRoot(), "harness", "src", "knowledge", "hardwareLookup.ts"),
                Path.Combine(RepositoryRoot(), "no-such-index.db"),
                Patience);

            var context = lookup.Describe("UR5e safety I/O");

            Assert.AreEqual(HardwareContextOutcome.Unavailable, context.Outcome);
            StringAssert.Contains(context.Reason, "no documentation index", StringComparison.Ordinal);
        }

        private static HarnessHardwareLookup LookupOrInconclusive()
        {
            var script = Path.Combine(RepositoryRoot(), "harness", "src", "knowledge", "hardwareLookup.ts");
            var index = Path.Combine(RepositoryRoot(), ".tia-mcp", "harness", "knowledge.db");

            if (!File.Exists(script))
            {
                Assert.Inconclusive($"No harness lookup at {script}.");
            }

            if (!File.Exists(index))
            {
                Assert.Inconclusive($"No documentation index at {index}. Build one with: npm run knowledge:index");
            }

            return new HarnessHardwareLookup(script, index, Patience);
        }

        /// <summary>Walks up from the test binary until the solution file says this is the root.</summary>
        /// <returns>The repository root.</returns>
        /// <exception cref="AssertInconclusiveException">The test is running outside a checkout.</exception>
        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TiaMcpServer.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                Assert.Inconclusive("Not running from inside a checkout, so the corpus cannot be found.");
            }

            return directory!.FullName;
        }
    }
}
