using System;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The gate is what stops two Openness calls from interleaving, which was measured to be a real
    /// thing and not a precaution: on 2026-08-18 two snapshot exports started 1 ms apart both ran from
    /// 1 ms to 1620 ms, each doing its own work.
    ///
    /// A lock needs no TIA Portal to be tested, which is the whole reason these live here. Nothing
    /// sleeps: every timing question is settled with an event the test controls.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class OpennessGateTests
    {
        // The gate is process-wide by design - there is one TIA Portal - so these cannot overlap with
        // each other. Hence DoNotParallelize on the class.

        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void Enter_WhileAnotherThreadHoldsIt_Waits()
        {
            // The property everything else rests on, asserted directly rather than inferred.
            using var holderIsIn = new ManualResetEventSlim(false);
            using var holderMayLeave = new ManualResetEventSlim(false);
            using var secondGotIn = new ManualResetEventSlim(false);

            var holder = Task.Run(() =>
            {
                using (OpennessGate.Enter())
                {
                    holderIsIn.Set();
                    holderMayLeave.Wait(Patience);
                }
            });

            Assert.IsTrue(holderIsIn.Wait(Patience), "the first thread never got in");

            var second = Task.Run(() =>
            {
                using (OpennessGate.Enter())
                {
                    secondGotIn.Set();
                }
            });

            Assert.IsFalse(
                secondGotIn.Wait(TimeSpan.FromMilliseconds(200)),
                "the second thread got in while the first was still holding the gate");

            holderMayLeave.Set();

            Assert.IsTrue(secondGotIn.Wait(Patience), "the second thread never got in after the gate was released");

            Task.WaitAll(holder, second);
        }

        [TestMethod]
        public void Enter_TwiceOnTheSameThread_IsAllowed()
        {
            // Re-entrancy is not a convenience. A job hands CompileSoftware to a worker, which calls
            // CompileSoftware, which takes the gate again on the same thread. A non-re-entrant gate
            // would deadlock on the very first job.
            using (OpennessGate.Enter())
            {
                using (OpennessGate.Enter())
                {
                    Assert.IsTrue(OpennessGate.IsHeldByCurrentThread);
                }

                Assert.IsTrue(OpennessGate.IsHeldByCurrentThread, "the inner lease released the outer one");
            }

            Assert.IsFalse(OpennessGate.IsHeldByCurrentThread);
        }

        [TestMethod]
        public void Dispose_Twice_IsNotAnError()
        {
            // A lease released twice would throw from inside a using block, hiding whatever the tool
            // was really reporting.
            var lease = OpennessGate.Enter();

            lease.Dispose();
            lease.Dispose();

            Assert.IsFalse(OpennessGate.IsHeldByCurrentThread);
        }

        [TestMethod]
        public void IsHeldByCurrentThread_IsFalseOnAThreadThatDidNotTakeIt()
        {
            // What McpServer.Portal checks. If this were true for a thread holding nothing, the
            // enforcement in that property would pass for a tool that never took the gate.
            using (OpennessGate.Enter())
            {
                var elsewhere = Task.Run(() => OpennessGate.IsHeldByCurrentThread);

                Assert.IsFalse(elsewhere.Result);
            }
        }

        [TestMethod]
        public void Run_ReturnsTheWorkResultAndReleasesTheGate()
        {
            var result = OpennessGate.Run(() => 42);

            Assert.AreEqual(42, result);
            Assert.IsFalse(OpennessGate.IsHeldByCurrentThread, "Run must not leak the lease");
        }

        [TestMethod]
        public void Run_WorkThatThrows_StillReleasesTheGate()
        {
            // A gate left held by a failed call is a server that never answers again, so this matters
            // more than the exception itself.
            Assert.ThrowsException<InvalidOperationException>(
                () => OpennessGate.Run<int>(() => throw new InvalidOperationException("boom")));

            Assert.IsFalse(OpennessGate.IsHeldByCurrentThread);
        }

        [TestMethod]
        public void Run_WithNoWork_IsRejected()
        {
            Assert.ThrowsException<ArgumentNullException>(() => OpennessGate.Run<int>(null!));
        }
    }
}
