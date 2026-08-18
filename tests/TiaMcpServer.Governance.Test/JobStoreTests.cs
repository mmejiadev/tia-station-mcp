using System;
using System.Collections.Generic;
using TiaMcpServer.Jobs;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// Jobs are not part of the governance layer, but they belong in this project rather than in the
    /// Openness suite for the same reason everything else here does: **they need no TIA Portal**, and
    /// that assembly starts one in <c>[AssemblyInitialize]</c> for every test in it. What this project
    /// really collects is the tests that must be runnable on any machine.
    ///
    /// Not one of these sleeps. Every timing question is settled by holding the work in a dispatcher
    /// the test controls, because a test that waits long enough to usually pass is a test that
    /// eventually fails for no reason and gets deleted.
    /// </remarks>
    [TestClass]
    public sealed class JobStoreTests
    {
        private static readonly DateTimeOffset Noon =
            new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void Start_ReturnsBeforeTheWorkRuns()
        {
            // The whole point of the feature: a download that once blocked this project for thirteen
            // hours must not be able to block the caller at all.
            var dispatcher = new HeldDispatcher();
            var store = new JobStore(new FixedClock(Noon), dispatcher);
            var ran = false;

            var id = store.Start("CompileSoftware", "PLC_0", () => { ran = true; return "done"; });

            Assert.IsFalse(ran, "Start must not run the work on the caller's thread");
            Assert.AreEqual(JobState.Queued, store.Status(id).State);
        }

        [TestMethod]
        public void Start_ThenRun_ReportsTheResult()
        {
            var dispatcher = new HeldDispatcher();
            var store = new JobStore(new FixedClock(Noon), dispatcher);

            var id = store.Start("CompileSoftware", "PLC_0", () => "compiled: 0 warning(s)");
            dispatcher.RunAll();

            var job = store.Status(id);

            Assert.AreEqual(JobState.Succeeded, job.State);
            Assert.AreEqual("compiled: 0 warning(s)", job.Detail);
            Assert.IsTrue(job.IsFinished);
        }

        [TestMethod]
        public void Start_WorkThatThrows_BecomesAFailedJobRatherThanALostException()
        {
            // Nothing is awaiting the worker thread, so an exception that escaped it would take the
            // only account of what went wrong with it.
            var dispatcher = new HeldDispatcher();
            var store = new JobStore(new FixedClock(Noon), dispatcher);

            var id = store.Start("DownloadToSimulation", "PLC_0", () =>
                throw new PortalException(PortalErrorCode.InvalidState, "no instance at that address"));
            dispatcher.RunAll();

            var job = store.Status(id);

            Assert.AreEqual(JobState.Failed, job.State);
            StringAssert.Contains(job.Detail, "no instance at that address", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Cancel_WhileQueued_MeansTheWorkNeverRuns()
        {
            // The one thing cancellation can honestly do, so it is asserted exactly rather than
            // hoped for: the dispatcher releases the work *after* the cancellation.
            var dispatcher = new HeldDispatcher();
            var store = new JobStore(new FixedClock(Noon), dispatcher);
            var ran = false;

            var id = store.Start("CompileSoftware", "PLC_0", () => { ran = true; return "compiled"; });
            var cancelled = store.Cancel(id);
            dispatcher.RunAll();

            Assert.AreEqual(JobState.Cancelled, cancelled.State);
            Assert.IsFalse(ran, "a cancelled job must not run, even once a worker picks it up");
            Assert.AreEqual(JobState.Cancelled, store.Status(id).State);
        }

        [TestMethod]
        public void Cancel_OnceFinished_ChangesNothingAndSaysSo()
        {
            var dispatcher = new HeldDispatcher();
            var store = new JobStore(new FixedClock(Noon), dispatcher);

            var id = store.Start("CompileSoftware", "PLC_0", () => "compiled");
            dispatcher.RunAll();

            var job = store.Cancel(id);

            Assert.AreEqual(JobState.Succeeded, job.State, "cancelling must not rewrite what already happened");
            Assert.IsFalse(job.IsCancellable);
        }

        [TestMethod]
        public void Status_AQueuedJob_IsTheOnlyOneReportedAsCancellable()
        {
            // Openness cannot interrupt a compile or a download once it has begun. Reporting a
            // running job as cancellable would be a promise nothing can keep, and the caller would
            // act on it.
            var dispatcher = new HeldDispatcher();
            var store = new JobStore(new FixedClock(Noon), dispatcher);

            var id = store.Start("CompileSoftware", "PLC_0", () => "compiled");

            Assert.IsTrue(store.Status(id).IsCancellable);

            dispatcher.RunAll();

            Assert.IsFalse(store.Status(id).IsCancellable);
        }

        [TestMethod]
        public void Status_AnUnknownJob_IsNotFound()
        {
            var store = new JobStore(new FixedClock(Noon), new HeldDispatcher());

            var exception = Assert.ThrowsException<PortalException>(() => store.Status(JobId.Create()));

            Assert.AreEqual(PortalErrorCode.NotFound, exception.Code);
        }

        [TestMethod]
        public void List_PutsTheNewestFirst()
        {
            var clock = new FixedClock(Noon);
            var store = new JobStore(clock, new HeldDispatcher());

            store.Start("CompileSoftware", "PLC_0", () => "first");
            clock.Advance(TimeSpan.FromMinutes(1));
            store.Start("DownloadToSimulation", "PLC_0", () => "second");

            var jobs = store.List();

            Assert.AreEqual(2, jobs.Count);
            Assert.AreEqual("DownloadToSimulation", jobs[0].Tool);
        }

        [TestMethod]
        public void List_BeforeAnyJob_IsEmptyRatherThanAFailure()
        {
            var store = new JobStore(new FixedClock(Noon), new HeldDispatcher());

            Assert.AreEqual(0, store.List().Count);
        }

        [TestMethod]
        public void Start_WithNoTool_IsRejected()
        {
            var store = new JobStore(new FixedClock(Noon), new HeldDispatcher());

            Assert.ThrowsException<ArgumentException>(() => store.Start(string.Empty, "PLC_0", () => "x"));
        }

        [TestMethod]
        public void Start_WithNoWork_IsRejected()
        {
            var store = new JobStore(new FixedClock(Noon), new HeldDispatcher());

            Assert.ThrowsException<ArgumentNullException>(() => store.Start("CompileSoftware", "PLC_0", null!));
        }

        [TestMethod]
        public void Parse_SomethingThatIsNotAJobId_IsInvalidParams()
        {
            var exception = Assert.ThrowsException<PortalException>(() => JobId.Parse("not-a-job"));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
        }

        [TestMethod]
        public void Constructor_WithNoDispatcher_IsRejected()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new JobStore(new FixedClock(Noon), null!));
        }

        /// <summary>A dispatcher that holds the work until the test releases it.</summary>
        private sealed class HeldDispatcher : IJobDispatcher
        {
            private readonly List<Action> _held = new List<Action>();

            public void Dispatch(Action work)
            {
                _held.Add(work);
            }

            internal void RunAll()
            {
                var pending = _held.ToArray();

                _held.Clear();

                foreach (var work in pending)
                {
                    work();
                }
            }
        }
    }
}
