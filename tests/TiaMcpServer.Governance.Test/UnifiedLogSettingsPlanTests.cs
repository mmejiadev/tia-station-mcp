using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: each single write is refused that leaves a segment larger or
    /// longer than its log, and the backup path is read-only while the backup is off. <see cref="FakeLog"/>
    /// refuses the same, so a plan it takes is one TIA Portal takes.
    /// </remarks>
    [TestClass]
    public sealed class UnifiedLogSettingsPlanTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 30, 16, 40, 0);

        [TestMethod]
        public void Steps_ManyRandomChanges_EveryWriteKeepsTheSegmentWithinTheLogAndEndsAtTheTarget()
        {
            var random = new Random(20261030);

            for (var round = 0; round < 500; round++)
            {
                var current = RandomSettings(random, null);
                var target = RandomSettings(random, current);
                var log = new FakeLog(current);

                foreach (var step in UnifiedLogSettingsPlan.Steps(current, target))
                {
                    log.Write(step);
                }

                CollectionAssert.AreEquivalent(UnifiedLogFieldValues.Of(target).ToList(), log.Values.ToList(), $"round {round}");
            }
        }

        /// <remarks>The case that broke a plan which widened the log only when the segment changed.</remarks>
        [TestMethod]
        public void Steps_ALogGrowingPastAnHour_NeverDropsBelowTheSegmentOnTheWay()
        {
            var segment = (100u, new UnifiedLogDuration(1, 3, 59, 0), Start);
            var current = new UnifiedLogSettings((1000u, new UnifiedLogDuration(1, 3, 59, 0)), segment, (false, string.Empty));
            var target = new UnifiedLogSettings((1000u, new UnifiedLogDuration(1, 4, 0, 0)), segment, (false, string.Empty));
            var log = new FakeLog(current);

            foreach (var step in UnifiedLogSettingsPlan.Steps(current, target))
            {
                log.Write(step);
            }

            Assert.AreEqual(4u, log.Values[UnifiedLogField.Hours]);
        }

        [TestMethod]
        public void Steps_NothingChanged_WritesNothing()
        {
            var settings = Settings(1000, 7, 100, 1, string.Empty);

            Assert.AreEqual(0, UnifiedLogSettingsPlan.Steps(settings, settings).Count);
        }

        /// <remarks>Found against TIA Portal on 2026-10-30: the path was written first and refused as read-only.</remarks>
        [TestMethod]
        public void Steps_ABackupTurnedOn_SwitchesItOnBeforeWritingThePath()
        {
            var steps = UnifiedLogSettingsPlan.Steps(Settings(1000, 7, 100, 1, string.Empty), Settings(1000, 7, 100, 1, @"D:\Backup"));

            CollectionAssert.AreEqual(new[] { UnifiedLogField.IsBackedUp, UnifiedLogField.BackupPath }, steps.Select(step => step.Field).ToArray());
        }

        /// <remarks>Turning off keeps the stored path, as TIA Portal does: only the switch is written.</remarks>
        [TestMethod]
        public void Steps_ABackupTurnedOff_OnlySwitchesItOff()
        {
            var steps = UnifiedLogSettingsPlan.Steps(Settings(1000, 7, 100, 1, @"D:\Backup"), Settings(1000, 7, 100, 1, @"D:\Backup", isBackedUp: false));

            CollectionAssert.AreEqual(new[] { UnifiedLogField.IsBackedUp }, steps.Select(step => step.Field).ToArray());
        }

        /// <remarks>
        /// The review of 2026-10-30: a log off with a stored path, put back after a failed change, lost that path. The
        /// path is written while the backup is still on, then the switch goes off.
        /// </remarks>
        [TestMethod]
        public void Steps_PuttingBackALogOffWithAStoredPath_WritesThePathBeforeSwitchingOff()
        {
            var steps = UnifiedLogSettingsPlan.Steps(Settings(1000, 7, 100, 1, @"D:\New"), Settings(1000, 7, 100, 1, @"D:\Old", isBackedUp: false));

            CollectionAssert.AreEqual(new object[] { @"D:\Old", false }, steps.Select(step => step.Value).ToArray());
        }

        /// <remarks>The review of 2026-10-30: a backup on with no path was read as no backup, and "" then changed nothing.</remarks>
        [TestMethod]
        public void Over_TurningOffABackupOnWithNoPath_SwitchesItOff()
        {
            var current = new UnifiedLogSettings((1000u, new UnifiedLogDuration(7, 0, 0, 0)), (100u, new UnifiedLogDuration(1, 0, 0, 0), Start), (true, string.Empty));
            var request = new UnifiedLogSettingsRequest("Log_Tolva", (null, null), (null, null, null), string.Empty);

            var steps = UnifiedLogSettingsPlan.Steps(current, request.Over(current));

            CollectionAssert.AreEqual(new[] { UnifiedLogField.IsBackedUp }, steps.Select(step => step.Field).ToArray());
        }

        [TestMethod]
        public void Check_ABackupOnWithNoPath_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsPlan.Check("Log_Tolva", Settings(1000, 7, 100, 1, string.Empty, isBackedUp: true)));

            StringAssert.Contains(refusal.Message, "backup is on with no path", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ASegmentOfNoTime_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsPlan.Check("Log_Tolva", Settings(1000, 7, 100, 0, string.Empty)));

            StringAssert.Contains(refusal.Message, "a segment needs", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ASegmentLargerThanTheLog_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsPlan.Check("Log_Tolva", Settings(100, 7, 200, 1, string.Empty)));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
            StringAssert.Contains(refusal.Message, "above the log's", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Check_ASegmentLongerThanTheLog_IsRefused()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => UnifiedLogSettingsPlan.Check("Log_Tolva", Settings(1000, 1, 100, 2, string.Empty)));

            StringAssert.Contains(refusal.Message, "longer than the log's", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Over_OnlyTheSettingsGiven_ChangeAndTheRestStay()
        {
            var request = new UnifiedLogSettingsRequest("Log_Tolva", (500u, null), (null, null, null), null);

            var after = request.Over(Settings(1000, 7, 100, 1, @"D:\Backup"));

            Assert.AreEqual(500u, after.MaxSize);
            Assert.AreEqual(7u, after.Duration.Days);
            Assert.AreEqual(@"D:\Backup", after.BackupPath);
        }

        private static UnifiedLogSettings Settings(uint maxSize, uint days, uint segmentSize, uint segmentDays, string backupPath, bool? isBackedUp = null)
        {
            return new UnifiedLogSettings((maxSize, new UnifiedLogDuration(days, 0, 0, 0)), (segmentSize, new UnifiedLogDuration(segmentDays, 0, 0, 0), Start), (isBackedUp ?? backupPath.Length > 0, backupPath));
        }

        /// <param name="random">The seeded generator.</param>
        /// <param name="current">For a target, the settings it changes: a target with the backup off keeps their stored path, as a request does.</param>
        [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "A seeded generator, so a failing round can be replayed; nothing secret is drawn.")]
        private static UnifiedLogSettings RandomSettings(Random random, UnifiedLogSettings? current)
        {
            var log = new UnifiedLogDuration((uint)random.Next(0, 4), (uint)random.Next(0, 24), (uint)random.Next(0, 60), (uint)random.Next(1, 60));
            var maxSize = (uint)random.Next(1, 2000);
            var segmentSeconds = (ulong)random.Next(0, (int)Math.Min(log.TotalSeconds, int.MaxValue) + 1);
            var segment = new UnifiedLogDuration((uint)(segmentSeconds / 86400), (uint)(segmentSeconds / 3600 % 24), (uint)(segmentSeconds / 60 % 60), (uint)(segmentSeconds % 60));
            var isOn = random.Next(0, 2) == 0;
            var path = current != null && !isOn ? current.BackupPath : random.Next(0, 3) == 0 && current == null ? string.Empty : $@"D:\Backup{random.Next(0, 3)}";

            return new UnifiedLogSettings((maxSize, log), ((uint)random.Next(0, (int)maxSize + 1), segment, Start.AddDays(random.Next(0, 3))), (isOn, path));
        }

        /// <summary>A log that refuses, as TIA Portal V20 does, each write leaving its segment above it.</summary>
        private sealed class FakeLog
        {
            public FakeLog(UnifiedLogSettings settings)
            {
                Values = UnifiedLogFieldValues.Of(settings);
            }

            public Dictionary<UnifiedLogField, object> Values { get; }

            public void Write((UnifiedLogField Field, object Value) step)
            {
                if (step.Field == UnifiedLogField.BackupPath && !(bool)Values[UnifiedLogField.IsBackedUp])
                {
                    Assert.Fail("TIA Portal would refuse the backup path: it is read-only while the backup is off");
                }

                var before = Values[step.Field];
                Values[step.Field] = step.Value;

                if (Size(UnifiedLogField.SegmentMaxSize) > Size(UnifiedLogField.MaxSize) || SegmentSeconds() > LogSeconds())
                {
                    Values[step.Field] = before;
                    Assert.Fail($"TIA Portal would refuse {step.Field} = {step.Value}: the segment would be above the log");
                }
            }

            private uint Size(UnifiedLogField field) => (uint)Values[field];

            private ulong LogSeconds() => Seconds(UnifiedLogField.Days, UnifiedLogField.Hours, UnifiedLogField.Minutes, UnifiedLogField.Seconds);

            private ulong SegmentSeconds() => Seconds(UnifiedLogField.SegmentDays, UnifiedLogField.SegmentHours, UnifiedLogField.SegmentMinutes, UnifiedLogField.SegmentSeconds);

            private ulong Seconds(UnifiedLogField days, UnifiedLogField hours, UnifiedLogField minutes, UnifiedLogField seconds)
            {
                return new UnifiedLogDuration(Size(days), Size(hours), Size(minutes), Size(seconds)).TotalSeconds;
            }
        }
    }
}
