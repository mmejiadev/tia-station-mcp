using System;
using System.Threading;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Makes one thread at a time reach TIA Portal.
    /// </summary>
    /// <remarks>
    /// **Openness runs concurrent calls in parallel, and nothing stops it.** Measured on 2026-08-18:
    /// two snapshot exports started 1 ms apart both ran from 1 ms to 1620 ms, each doing its own
    /// work. Had COM been marshalling them into one apartment the second would have begun when the
    /// first ended. It does not, so two operations really do interleave inside TIA Portal, and the
    /// engineering API is nowhere documented as safe for that.
    ///
    /// It did not matter until this session. The transport is stdio, one request at a time, so
    /// nothing ran concurrently. Asynchronous jobs changed that: a compile running on a worker thread
    /// can now coincide with the next request. This gate is what makes that safe rather than lucky.
    ///
    /// **Re-entrant on purpose.** A tool called from inside a job calls back through the same tools
    /// (<c>runAsJob</c> hands `CompileSoftware` to a worker, which calls `CompileSoftware`), and
    /// <see cref="Monitor"/> lets the owning thread back in. A non-re-entrant gate would deadlock on
    /// the first job.
    ///
    /// **Nothing that does not touch Openness may take it.** `GetJobStatus` and `ListJobs` above all:
    /// if polling had to queue behind the compile it is polling, asynchronous jobs would be pointless.
    ///
    /// **A tool must not hold it across an <c>await</c> that moves to another thread.** `Monitor` is
    /// owned by a thread, so the continuation would not own it and the work would deadlock against
    /// its own caller. That is why `ImportBlocksFromDocuments` no longer wraps its import in
    /// <c>Task.Run</c> — which was also a latent defect of its own, since that made an Openness call
    /// from a thread-pool thread.
    /// </remarks>
    public static class OpennessGate
    {
        private static readonly object Sync = new object();

        /// <summary>Waits for exclusive access to TIA Portal, and releases it on dispose.</summary>
        /// <returns>The lease. Dispose it, or nothing else ever reaches TIA Portal again.</returns>
        /// <remarks>
        /// A lease rather than a <c>Run(Action)</c> wrapper so a tool takes it in one line at the top
        /// of its body, without its whole body being re-indented into a lambda. The intent is that a
        /// reader can see, in the first line of a tool, whether it touches TIA Portal.
        /// </remarks>
        public static OpennessLease Enter()
        {
            Monitor.Enter(Sync);

            return new OpennessLease(Sync);
        }

        /// <summary>Runs one piece of work with exclusive access to TIA Portal.</summary>
        /// <typeparam name="TResult">What the work produces.</typeparam>
        /// <param name="work">The work. It must not await anything.</param>
        /// <returns>Whatever the work returned.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="work"/> is null.</exception>
        /// <remarks>
        /// The form for a lambda, where <see cref="Enter"/> cannot be used without turning an
        /// expression into a block. The asynchronous export tools hand each Openness call to a worker
        /// thread one at a time, and this is what wraps them.
        ///
        /// **What it guarantees is one Openness *call* at a time, not one logical operation.** A bulk
        /// export makes many calls, and a compile can land between two of them. Holding the gate
        /// across a whole batch would block every other request for minutes, which is a worse trade
        /// for a read; the concurrency that was measured and that this removes is two calls running
        /// at the same instant.
        /// </remarks>
        public static TResult Run<TResult>(Func<TResult> work)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            using (Enter())
            {
                return work();
            }
        }

        /// <summary>Whether the calling thread is already inside the gate.</summary>
        /// <remarks>
        /// For tests and diagnostics. Production code should take the lease and let re-entrancy do
        /// its work rather than branch on this: a check followed by an action is a race, and a gate
        /// that is sometimes taken is not a gate.
        /// </remarks>
        public static bool IsHeldByCurrentThread => Monitor.IsEntered(Sync);
    }
}
