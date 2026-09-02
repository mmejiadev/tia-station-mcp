using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The audit trail was append-only because nothing in this code rewrote it — a statement about
    /// the code, not about the file. These tests are about the file: each one edits it the way a
    /// person with a text editor would, and asserts that the chain notices.
    ///
    /// A chain that has never been shown to catch a forgery is decoration, so every test here
    /// tampers first and checks second.
    /// </remarks>
    [TestClass]
    public class AuditChainTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

        private string _path = string.Empty;

        [TestInitialize]
        public void CreateTrail()
        {
            _path = Path.Combine(Path.GetTempPath(), $"audit-chain-{Guid.NewGuid():N}.jsonl");
        }

        [TestCleanup]
        public void RemoveTrail()
        {
            File.Delete(_path);
        }

        [TestMethod]
        public void VerifyChain_UntouchedTrail_IsIntact()
        {
            var report = WriteEntries(4).VerifyChain();

            Assert.IsTrue(report.IsIntact, report.ToString());
            Assert.AreEqual(4, report.Chained);
            Assert.AreEqual(0, report.Unchained);
        }

        [TestMethod]
        public void VerifyChain_EntryEditedInPlace_NamesTheLine()
        {
            // The forgery this exists for: somebody changes what a write actually did.
            WriteEntries(4);
            Rewrite(2, line => line.Replace("\"target\":\"PLC_0/Blocks/FB_2\"", "\"target\":\"PLC_0/Blocks/FB_9\""));

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsFalse(report.IsIntact);
            Assert.AreEqual(2, report.BrokenAtLine);
            StringAssert.Contains(report.Reason, "edited after it was written", StringComparison.Ordinal);
        }

        [TestMethod]
        public void VerifyChain_EntryRemoved_NamesTheGap()
        {
            // Deleting the record of an action is the tampering that leaves the file most plausible:
            // every remaining line is genuine.
            WriteEntries(4);
            RemoveLine(2);

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsFalse(report.IsIntact);
            StringAssert.Contains(report.Reason, "removed or inserted", StringComparison.Ordinal);
        }

        [TestMethod]
        public void VerifyChain_EntriesReordered_IsNoticed()
        {
            WriteEntries(4);
            Swap(2, 3);

            Assert.IsFalse(new JsonlAuditTrail(_path).VerifyChain().IsIntact);
        }

        [TestMethod]
        public void VerifyChain_ChainFieldsStrippedFromTheLastEntry_IsNoticed()
        {
            // The forgery that used to work: edit the last entry, then delete its chain fields so
            // the edit reads as history from before chaining existed. Nothing follows it to give it
            // away by a gap in the sequence, so before 2026-09-02 this file reported intact.
            WriteEntries(3);
            Rewrite(3, WithoutChainFields);

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.IsFalse(report.IsIntact, report.ToString());
            Assert.AreEqual(3, report.BrokenAtLine);
        }

        [TestMethod]
        public void VerifyChain_ChainFieldsStrippedFromAnEntry_BlamesThatEntryRatherThanTheNextOne()
        {
            // Stripping an entry in the middle was always caught, but for the wrong reason: the
            // *following* entry was reported as removed or inserted, because its sequence no longer
            // followed. A check whose only value is that people believe it has to name the right
            // line and the right crime.
            WriteEntries(3);
            Rewrite(2, WithoutChainFields);

            var report = new JsonlAuditTrail(_path).VerifyChain();

            Assert.AreEqual(2, report.BrokenAtLine);
            StringAssert.Contains(report.Reason, "stripped from it", StringComparison.Ordinal);
        }

        [TestMethod]
        public void VerifyChain_TrailWrittenBeforeChainingExisted_ReportsItUnattestedRatherThanBroken()
        {
            // Chaining was added to a file that already held thousands of entries. Reporting those
            // as tampered with would be false; reporting them as verified would be worse.
            File.WriteAllText(_path, "{\"timestamp\":\"2026-08-01T10:00:00.0000000+00:00\",\"planId\":\"OLD-1\"," +
                "\"mode\":\"Study\",\"tool\":\"WriteScl\",\"target\":\"PLC_0/Blocks/FB_Old\",\"value\":\"\"," +
                "\"backupPath\":\"\",\"origin\":\"agent\",\"outcome\":\"Applied\",\"detail\":\"\"}" + Environment.NewLine);

            var trail = new JsonlAuditTrail(_path);
            trail.Append(Entry(1));

            var report = trail.VerifyChain();

            Assert.IsTrue(report.IsIntact, report.ToString());
            Assert.AreEqual(1, report.Unchained);
            Assert.AreEqual(1, report.Chained);
            StringAssert.Contains(report.ToString(), "not attested", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Append_AfterReopeningTheFile_ContinuesTheSameChain()
        {
            // The server is restarted between sessions. A new instance that started its own chain
            // would silently split the trail into two halves that vouch for nothing across the join.
            WriteEntries(2);

            var second = new JsonlAuditTrail(_path);
            second.Append(Entry(3));

            var report = second.VerifyChain();

            Assert.IsTrue(report.IsIntact, report.ToString());
            Assert.AreEqual(3, report.Chained);
        }

        [TestMethod]
        public void Read_ChainedTrail_StillReturnsTheEntries()
        {
            // The chain fields are extra keys on the line. Every existing reader has to keep working.
            WriteEntries(3);

            var entries = new JsonlAuditTrail(_path).Read();

            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual("PLC_0/Blocks/FB_1", entries[0].Target);
        }

        [TestMethod]
        public void VerifyChain_NoTrailAtAll_IsIntactAndEmpty()
        {
            var report = new JsonlAuditTrail(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.jsonl")).VerifyChain();

            Assert.IsTrue(report.IsIntact);
            Assert.AreEqual(0, report.Chained);
        }

        private JsonlAuditTrail WriteEntries(int count)
        {
            var trail = new JsonlAuditTrail(_path);

            for (var index = 1; index <= count; index++)
            {
                trail.Append(Entry(index));
            }

            return trail;
        }

        private static AuditEntry Entry(int index)
        {
            var request = new ChangeRequest("WriteScl", $"PLC_0/Blocks/FB_{index}", string.Empty, "test");
            var plan = new ChangePlan(PlanId.Create(), request, OperationMode.Study, Now.AddMinutes(10));

            return new AuditEntry(Now, plan, AuditOutcome.Applied, string.Empty);
        }

        /// <summary>One line with its chain fields removed, as a forger would leave it.</summary>
        /// <param name="line">The line as it was written.</param>
        /// <returns>The same entry, with no seq, prev or hash.</returns>
        private static string WithoutChainFields(string line)
        {
            var record = JsonSerializer.Deserialize<Dictionary<string, string>>(line);

            record!.Remove("seq");
            record.Remove("prev");
            record.Remove("hash");

            return JsonSerializer.Serialize(record);
        }

        private void Rewrite(int lineNumber, Func<string, string> edit)
        {
            var lines = File.ReadAllLines(_path);
            lines[lineNumber - 1] = edit(lines[lineNumber - 1]);
            File.WriteAllLines(_path, lines);
        }

        private void RemoveLine(int lineNumber)
        {
            var lines = File.ReadAllLines(_path).ToList();
            lines.RemoveAt(lineNumber - 1);
            File.WriteAllLines(_path, lines);
        }

        private void Swap(int first, int second)
        {
            var lines = File.ReadAllLines(_path);
            (lines[first - 1], lines[second - 1]) = (lines[second - 1], lines[first - 1]);
            File.WriteAllLines(_path, lines);
        }
    }
}
