using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance.Tests
{
    /// <summary>
    /// The audit trail is written in C# and verified in TypeScript, by the harness and by the web
    /// platform's importer; this golden trail is the contract between them for version 3.
    /// </summary>
    /// <remarks>
    /// The harness verifies <c>harness/test/assets/audit-chain-golden-v3.jsonl</c>, and this test
    /// asserts the server writes exactly that file, hashes included. Version 2's golden trail was
    /// produced the same way. When the two drift apart, the actual output is left in the temporary
    /// directory and the failure names it, so the difference can be read rather than guessed.
    /// </remarks>
    [TestClass]
    public sealed class AuditTrailContractTests
    {
        private const string GoldenName = "audit-chain-golden-v3.jsonl";
        private const string OpenProject = @"C:\Projects\Cell\Cell.ap20";

        [TestMethod]
        public void AuditTrail_AsTheServerWritesVersionThree_IsTheGoldenTrailTheHarnessVerifies()
        {
            // Its own file every run: worktrees of this repository run the suite side by side.
            var written = Path.Combine(Path.GetTempPath(), $"audit-chain-golden-v3.{Guid.NewGuid():N}.actual.jsonl");

            var trail = new JsonlAuditTrail(written);
            trail.Append(Entry("AAA-111", new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), AuditOutcome.Planned, OpenProject));
            trail.Append(Entry("AAA-111", new DateTimeOffset(2026, 10, 4, 12, 0, 5, TimeSpan.Zero), AuditOutcome.Applied, OpenProject));
            trail.Append(Entry("BBB-222", new DateTimeOffset(2026, 10, 4, 12, 5, 0, TimeSpan.Zero), AuditOutcome.Refused, string.Empty));

            CollectionAssert.AreEqual(
                LinesOf(GoldenPath()),
                LinesOf(written),
                $"The server no longer writes the golden trail. What it wrote is in {written}.");

            // Kept only when it differs, because then it is the evidence.
            File.Delete(written);
        }

        private static AuditEntry Entry(string planId, DateTimeOffset timestamp, AuditOutcome outcome, string project)
        {
            var request = new ChangeRequest("WriteScl", "PLC_1/Blocks/FC_Motor", string.Empty, "agent")
                .WithDocumentation(HardwareContext.Unavailable("no documentation index on this machine"))
                .WithProject(project);
            var plan = new ChangePlan(PlanId.Parse(planId), request, OperationMode.Study, timestamp.AddMinutes(10));

            return new AuditEntry(timestamp, plan, outcome, string.Empty);
        }

        /// <remarks>Line endings are not part of the contract: Git checks the file out with CRLF.</remarks>
        private static string[] LinesOf(string path)
        {
            return File.Exists(path)
                ? File.ReadAllLines(path).Where(line => line.Trim().Length > 0).ToArray()
                : Array.Empty<string>();
        }

        private static string GoldenPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TiaMcpServer.sln")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                Assert.Inconclusive("Not running from inside a checkout, so the golden trail cannot be found.");
            }

            return Path.Combine(directory!.FullName, "harness", "test", "assets", GoldenName);
        }
    }
}
