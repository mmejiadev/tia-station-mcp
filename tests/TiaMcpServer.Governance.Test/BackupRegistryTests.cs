using System;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The rule under test is the one the roadmap states as a sentence: **a backup the caller can
    /// forget to ask for is not a backup.** These assert the two halves that make it true — the
    /// caller cannot choose the location, and everything saved can be enumerated afterwards.
    ///
    /// No TIA Portal, like everything else in this project. A backup registry is filesystem work.
    /// </remarks>
    [TestClass]
    public sealed class BackupRegistryTests
    {
        private static readonly DateTimeOffset Noon =
            new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        private string _root = string.Empty;

        [TestInitialize]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "TiaMcpBackupTests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void RemoveRoot()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [TestMethod]
        public void Allocate_UnderAMissingRoot_CreatesTheDirectory()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            var path = registry.Allocate("WriteScl", "PLC_0/Blocks");

            Assert.IsTrue(Directory.Exists(path), "the export has nowhere to write unless this exists on return");
            StringAssert.StartsWith(path, _root, StringComparison.Ordinal);
        }

        [TestMethod]
        public void Allocate_NamesTheDirectoryAfterTheMomentAndTheTool()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            var path = registry.Allocate("WriteScl", "PLC_0/Blocks");

            // Timestamped and attributed, because the person looking for it later knows roughly when
            // it happened and what they ran, and nothing else.
            StringAssert.Contains(Path.GetFileName(path), "20260818-120000", StringComparison.Ordinal);
            StringAssert.Contains(Path.GetFileName(path), "WriteScl", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Allocate_TwiceInTheSameSecond_KeepsThemApart()
        {
            // Not hypothetical: two writes to the same program back to back land in the same second,
            // and sharing a directory would mix one change's previous state into another's.
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            var first = registry.Allocate("WriteScl", "PLC_0/Blocks");
            var second = registry.Allocate("WriteScl", "PLC_0/Blocks");

            Assert.AreNotEqual(first, second);
            Assert.IsTrue(Directory.Exists(first));
            Assert.IsTrue(Directory.Exists(second));
        }

        [TestMethod]
        public void Allocate_WithASlashInTheTarget_StillProducesOneDirectory()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            var path = registry.Allocate("WriteScl", "Group1/PLC_1/Blocks/FB_Station");

            Assert.AreEqual(_root, Path.GetDirectoryName(path), "a target path must not become a directory tree");
        }

        [TestMethod]
        public void Allocate_WithNoTool_IsRejected()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            Assert.ThrowsException<ArgumentException>(() => registry.Allocate(string.Empty, "PLC_0"));
        }

        [TestMethod]
        public void Allocate_WithNoTarget_IsRejected()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            Assert.ThrowsException<ArgumentException>(() => registry.Allocate("WriteScl", string.Empty));
        }

        [TestMethod]
        public void Allocate_WhenTheRootCannotBeCreated_ReportsItAsAnOperationFailure()
        {
            // A file where the root should be is the cheapest stand-in for a read-only or full disk.
            // It must not surface as an ArgumentException the caller would read as bad input.
            var blocked = Path.Combine(_root, "occupied");
            Directory.CreateDirectory(_root);
            File.WriteAllText(blocked, "not a directory");

            var registry = new BackupRegistry(blocked, new FixedClock(Noon));

            var exception = Assert.ThrowsException<PortalException>(() => registry.Allocate("WriteScl", "PLC_0"));

            Assert.AreEqual(PortalErrorCode.ExportFailed, exception.Code);
        }

        [TestMethod]
        public void List_AfterAllocating_FindsItWithItsToolAndTarget()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));
            registry.Allocate("WriteScl", "Group1/PLC_1/Blocks/FB_Station");

            var backups = registry.List();

            Assert.AreEqual(1, backups.Count);
            Assert.AreEqual("WriteScl", backups[0].Tool);

            // The untruncated, unsanitised target: the directory name is short and safe, the record
            // is what has to be accurate.
            Assert.AreEqual("Group1/PLC_1/Blocks/FB_Station", backups[0].Target);
            Assert.AreEqual(Noon, backups[0].TakenAt);
        }

        [TestMethod]
        public void List_AnAllocationNothingWasExportedInto_ReportsItAsEmpty()
        {
            // This is what a refused or failed change leaves behind, and it must not read as a
            // backup somebody could restore from. The manifest does not count as content.
            var registry = new BackupRegistry(_root, new FixedClock(Noon));
            registry.Allocate("WriteScl", "PLC_0");

            var backups = registry.List();

            Assert.AreEqual(0, backups[0].FileCount);
            Assert.IsTrue(backups[0].IsEmpty);
        }

        [TestMethod]
        public void List_AfterSomethingWasExported_CountsTheFiles()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));
            var path = registry.Allocate("WriteScl", "PLC_0");

            File.WriteAllText(Path.Combine(path, "FB_Station.scl"), "FUNCTION_BLOCK \"FB_Station\"");
            Directory.CreateDirectory(Path.Combine(path, "types"));
            File.WriteAllText(Path.Combine(path, "types", "UDT_Piece.udt"), "TYPE \"UDT_Piece\"");

            var backups = registry.List();

            Assert.AreEqual(2, backups[0].FileCount, "the manifest must not be counted as saved state");
            Assert.IsFalse(backups[0].IsEmpty);
        }

        [TestMethod]
        public void List_WithSeveralBackups_PutsTheNewestFirst()
        {
            var clock = new FixedClock(Noon);
            var registry = new BackupRegistry(_root, clock);

            registry.Allocate("WriteScl", "PLC_0");
            clock.Advance(TimeSpan.FromMinutes(5));
            registry.Allocate("CreateIoSystem", "PLC_0");

            var backups = registry.List();

            Assert.AreEqual("CreateIoSystem", backups[0].Tool);
        }

        [TestMethod]
        public void List_BeforeAnythingWasSaved_IsEmptyRatherThanAFailure()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));

            Assert.AreEqual(0, registry.List().Count);
        }

        [TestMethod]
        public void List_ADirectoryTheRegistryDidNotCreate_IsLeftOut()
        {
            // Someone else's folder under the root, or one half-created by a process that died. It
            // cannot be attributed to a tool or a target, and inventing one would be worse than
            // omitting it.
            var registry = new BackupRegistry(_root, new FixedClock(Noon));
            registry.Allocate("WriteScl", "PLC_0");
            Directory.CreateDirectory(Path.Combine(_root, "not-a-backup"));

            var backups = registry.List();

            Assert.AreEqual(1, backups.Count);
        }

        [TestMethod]
        public void List_WithACorruptManifest_StillReportsTheOthers()
        {
            var registry = new BackupRegistry(_root, new FixedClock(Noon));
            var corrupt = registry.Allocate("WriteScl", "PLC_0");
            registry.Allocate("CreateIoSystem", "PLC_0");

            File.WriteAllText(Path.Combine(corrupt, "backup.json"), "{ truncated");

            var backups = registry.List();

            Assert.AreEqual(1, backups.Count, "one unreadable manifest must not hide every other backup");
            Assert.AreEqual("CreateIoSystem", backups[0].Tool);
        }

        [TestMethod]
        public void Constructor_WithNoRoot_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => new BackupRegistry(string.Empty, new FixedClock(Noon)));
        }

        [TestMethod]
        public void Constructor_WithNoClock_IsRejected()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new BackupRegistry(_root, null!));
        }
    }
}
