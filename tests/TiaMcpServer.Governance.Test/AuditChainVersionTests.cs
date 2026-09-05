using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiaMcpServer.Knowledge;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The chain hashes an entry's values as a JSON array, so an extra value changes the hash of
    /// every entry written without it. Until 2026-09-05 the trail's own documentation promised the
    /// opposite - that a field appended to the end would leave earlier entries verifiable - and
    /// adding the citation for audit finding F3 would have made every existing trail report as
    /// edited after the fact, which is the strongest alarm this system has.
    ///
    /// So a line records the version of the canonical form it was written under. These tests are
    /// what keeps that true the next time a field is added.
    /// </remarks>
    [TestClass]
    public sealed class AuditChainVersionTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// One line exactly as the server wrote it before chain versioning existed: ten fields, no
        /// version, and the hash it was given at the time.
        /// </summary>
        private const string VersionOneLine =
            "{\"timestamp\":\"2026-08-29T12:00:00.0000000\\u002B00:00\",\"planId\":\"AAA-111\",\"mode\":\"Study\"," +
            "\"tool\":\"WriteScl\",\"target\":\"PLC_0/Blocks/FB_Estacion_1\",\"value\":\"\",\"backupPath\":\"\"," +
            "\"origin\":\"agent\",\"outcome\":\"Applied\",\"detail\":\"\",\"seq\":\"1\",\"prev\":\"\"," +
            "\"hash\":\"7607e107cc1c8b783dd2508df865ce2b15f72d42fb8c66bebe11f676f918c985\"}";

        private string _path = string.Empty;

        [TestInitialize]
        public void CreateTrail()
        {
            _path = Path.Combine(Path.GetTempPath(), $"audit-version-{Guid.NewGuid():N}.jsonl");
        }

        [TestCleanup]
        public void RemoveTrail()
        {
            File.Delete(_path);
        }

        /// <remarks>
        /// The reason the whole thing exists. This line was produced by the server as it stood
        /// before the citation field, hash included, and it has to keep verifying for ever: a trail
        /// somebody recorded a workshop session into does not get to become unverifiable because a
        /// later version of the server learned to record one more thing.
        /// </remarks>
        [TestMethod]
        public void VerifyChain_ALineWrittenBeforeTheFieldExisted_StillVerifies()
        {
            File.WriteAllLines(_path, new[] { VersionOneLine });

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsTrue(report.IsIntact, report.Reason);
            Assert.AreEqual(1, report.Chained);
        }

        [TestMethod]
        public void VerifyChain_OldLinesFollowedByNewOnes_AreAllIntact()
        {
            File.WriteAllLines(_path, new[] { VersionOneLine });

            var trail = new JsonlAuditTrail(_path);
            trail.Append(Entry(AnUnavailableLookup()));
            trail.Append(Entry(AnUnavailableLookup()));

            var report = trail.VerifyChain();

            Assert.IsTrue(report.IsIntact, report.Reason);
            Assert.AreEqual(3, report.Chained);
        }

        [TestMethod]
        public void Append_AnEntry_RecordsTheVersionItWasWrittenUnder()
        {
            new JsonlAuditTrail(_path).Append(Entry(AnUnavailableLookup()));

            Assert.AreEqual("2", Record(1)["v"]);
        }

        /// <remarks>
        /// The version is not itself hashed and does not need to be: changing it makes the entry
        /// verify against the wrong list of fields, and the hash stops matching. It fails closed.
        /// </remarks>
        [TestMethod]
        public void VerifyChain_AnEntryWhoseVersionWasChanged_IsCaught()
        {
            new JsonlAuditTrail(_path).Append(Entry(AnUnavailableLookup()));
            Rewrite(1, line => line.Replace("\"v\":\"2\"", "\"v\":\"1\""));

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsFalse(report.IsIntact);
            StringAssert.Contains(report.Reason, "do not match its hash", StringComparison.Ordinal);
        }

        /// <remarks>
        /// A trail written by a newer server should say so rather than be called a forgery. The two
        /// are different problems and the person reading the verdict has to be able to tell.
        /// </remarks>
        [TestMethod]
        public void VerifyChain_AVersionThisServerDoesNotKnow_SaysSoRatherThanCryingTampering()
        {
            new JsonlAuditTrail(_path).Append(Entry(AnUnavailableLookup()));
            Rewrite(1, line => line.Replace("\"v\":\"2\"", "\"v\":\"99\""));

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsFalse(report.IsIntact);
            StringAssert.Contains(report.Reason, "written by a newer one", StringComparison.Ordinal);
        }

        /// <remarks>
        /// Audit finding F3: the plan showed the citation to whoever confirmed the change, and the
        /// trail did not keep it. Reading it back matters as much as writing it - the summary is
        /// lossy, so a plan rebuilt from a line cannot regenerate it.
        /// </remarks>
        [TestMethod]
        public void Read_AnEntryWithACitation_KeepsItThroughTheFile()
        {
            var trail = new JsonlAuditTrail(_path);
            trail.Append(Entry(HardwareContext.Cited(new[] { ACitation() })));

            var written = trail.Read()[0].Documentation;

            StringAssert.Contains(written, "page 47", StringComparison.Ordinal);
            StringAssert.Contains(written, "UR5e", StringComparison.Ordinal);
        }

        /// <remarks>
        /// A change made with nothing behind it is a fact about that change, so the trail records
        /// the absence as plainly as it records a citation.
        /// </remarks>
        [TestMethod]
        public void Read_AnEntryWithNoDocumentation_SaysSoRatherThanLeavingItBlank()
        {
            var trail = new JsonlAuditTrail(_path);
            trail.Append(Entry(HardwareContext.NotFound()));

            Assert.AreNotEqual(string.Empty, trail.Read()[0].Documentation);
        }

        /// <remarks>
        /// The citation is inside the hash, which is the whole reason for versioning the chain
        /// rather than writing the field outside it. A record of what justified a change that
        /// anybody could rewrite afterwards would justify nothing.
        /// </remarks>
        [TestMethod]
        public void VerifyChain_AnEntryWhoseCitationWasEdited_IsCaught()
        {
            var trail = new JsonlAuditTrail(_path);
            trail.Append(Entry(HardwareContext.Cited(new[] { ACitation() })));

            Rewrite(1, line => line.Replace("page 47", "page 48"));

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsFalse(report.IsIntact);
            StringAssert.Contains(report.Reason, "do not match its hash", StringComparison.Ordinal);
        }

        private static AuditEntry Entry(HardwareContext documentation)
        {
            var request = new ChangeRequest("WriteScl", "PLC_0/Blocks/FB_1", string.Empty, "test")
                .WithDocumentation(documentation);
            var plan = new ChangePlan(PlanId.Create(), request, OperationMode.Study, Now.AddMinutes(10));

            return new AuditEntry(Now, plan, AuditOutcome.Applied, string.Empty);
        }

        private static HardwareContext AnUnavailableLookup()
        {
            return HardwareContext.Unavailable("no documentation index on this machine");
        }

        private static HardwareCitation ACitation()
        {
            var document = new SourceDocument("UR5e", "Universal Robots e-Series User Manual UR5e", "SW 5.16");

            return new HardwareCitation(document, 47, "configurable I/O can be set as safety-related");
        }

        private Dictionary<string, string> Record(int lineNumber)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllLines(_path)[lineNumber - 1])!;
        }

        private void Rewrite(int lineNumber, Func<string, string> edit)
        {
            var lines = File.ReadAllLines(_path);
            lines[lineNumber - 1] = edit(lines[lineNumber - 1]);
            File.WriteAllLines(_path, lines);
        }
    }
}
