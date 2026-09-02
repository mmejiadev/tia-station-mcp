using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The governance layer is reached from two threads, and until 2026-08-29 nothing said so.
    /// A write started through <c>JobStore.Start</c> runs on the thread pool while the thread
    /// serving the protocol confirms a plan, so the plan store and the audit trail are shared
    /// mutable state on the one path every write takes.
    ///
    /// These tests are the reason the locks may not be removed. They are deliberately written as
    /// races rather than as assertions about locks: a test that asserted "there is a lock" would
    /// pass against a lock that guards the wrong thing.
    ///
    /// A passing concurrency test proves less than a failing one, so these were checked the only
    /// way that means anything: the locks were removed and the suite was run again. **All five
    /// failed, none passed.** Two of the failures are worth recording, because they are what the
    /// locks now prevent:
    ///
    /// - <c>Add</c> threw <c>ArgumentException: destination array is not long enough</c> from inside
    ///   <c>Dictionary.Resize</c> - the dictionary corrupting its own storage, not a clean error.
    /// - <c>Take</c> handed the same plan to more than one thread on 7 of 200 rounds. In production
    ///   that is one approval running an approved change twice.
    /// </remarks>
    [TestClass]
    public class ConcurrentWriteTests
    {
        private const int Writers = 32;
        private const int PerWriter = 16;

        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void Add_ManyThreadsAtOnce_LosesNoPlan()
        {
            // An unsynchronised Dictionary under parallel writes drops entries silently, and a
            // dropped plan is a confirmation that will never find its work.
            var store = new ChangePlanStore(new FixedClock(Now));
            var plans = Enumerable.Range(0, Writers * PerWriter).Select(_unused => Plan()).ToArray();

            Parallel.ForEach(plans, plan => store.Add(plan, () => "written"));

            Assert.AreEqual(plans.Length, store.Pending().Count);
        }

        [TestMethod]
        public void Take_TwoThreadsConfirmingTheSamePlan_HandsItToExactlyOne()
        {
            // The invariant the whole store exists for: a confirmation is spent when it is used.
            // Handing the same plan to two threads would run one approved change twice.
            var store = new ChangePlanStore(new FixedClock(Now));
            var taken = new ConcurrentBag<PlanId>();
            var refused = 0;

            for (var round = 0; round < 200; round++)
            {
                var plan = Plan();
                store.Add(plan, () => "written");

                Parallel.For(0, 8, _unused =>
                {
                    try
                    {
                        taken.Add(store.Take(plan.Id).Plan.Id);
                    }
                    catch (PortalException)
                    {
                        System.Threading.Interlocked.Increment(ref refused);
                    }
                });
            }

            Assert.AreEqual(200, taken.Count, "each plan must be handed out exactly once");
            Assert.AreEqual(200 * 7, refused, "every other attempt must be told the plan is not waiting");
        }

        [TestMethod]
        public void AddAndTake_Interleaved_LeavesTheStoreConsistent()
        {
            // Adding while another thread takes is the actual production shape: a job proposes a
            // change while the protocol thread confirms an earlier one.
            var store = new ChangePlanStore(new FixedClock(Now));
            var plans = Enumerable.Range(0, Writers * PerWriter).Select(_unused => Plan()).ToArray();
            var taken = 0;

            Parallel.ForEach(plans, plan =>
            {
                store.Add(plan, () => "written");

                try
                {
                    store.Take(plan.Id);
                    System.Threading.Interlocked.Increment(ref taken);
                }
                catch (PortalException)
                {
                    Assert.Fail("a plan this thread had just added was not waiting");
                }
            });

            Assert.AreEqual(plans.Length, taken);
            Assert.AreEqual(0, store.Pending().Count, "everything added was taken, so nothing may remain");
        }

        [TestMethod]
        public void Pending_WhileOtherThreadsWrite_DoesNotThrow()
        {
            // Enumerating a Dictionary that another thread is mutating throws
            // InvalidOperationException, which here would surface as the mode banner failing to
            // render while a job is running - a defect that looks like a dashboard bug.
            var store = new ChangePlanStore(new FixedClock(Now));
            var readers = Task.Run(() =>
            {
                for (var i = 0; i < 2000; i++)
                {
                    store.Pending();
                }
            });

            Parallel.For(0, Writers * PerWriter, _unused => store.Add(Plan(), () => "written"));

            readers.GetAwaiter().GetResult();
        }

        [TestMethod]
        public void Append_ManyThreadsAtOnce_WritesEveryLineWhole()
        {
            // Two unsynchronised appends to one file can interleave into a line no reader can
            // parse, or fail outright on a sharing violation. Either loses audit evidence, which
            // is the one failure the audit trail exists to make impossible.
            var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.jsonl");
            var trail = new JsonlAuditTrail(path);

            try
            {
                Parallel.For(0, Writers * PerWriter, index => trail.Append(Entry(index)));

                var lines = File.ReadAllLines(path);

                Assert.AreEqual(Writers * PerWriter, lines.Length, "every append must produce exactly one line");
                Assert.AreEqual(Writers * PerWriter, trail.Read().Count, "and every line must still be readable");
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static ChangePlan Plan()
        {
            var request = new ChangeRequest("WriteScl", "PLC_0/Blocks/FB_Station", "FUNCTION_BLOCK", "test");

            return new ChangePlan(PlanId.Create(), request, OperationMode.Study, Now.AddMinutes(10));
        }

        private static AuditEntry Entry(int index)
        {
            var request = new ChangeRequest("WriteScl", $"PLC_0/Blocks/FB_{index}", "FUNCTION_BLOCK", "test");
            var plan = new ChangePlan(PlanId.Create(), request, OperationMode.Study, Now.AddMinutes(10));

            return new AuditEntry(Now, plan, AuditOutcome.Applied, $"entry {index}");
        }
    }
}
