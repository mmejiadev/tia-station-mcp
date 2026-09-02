using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Governance;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Jobs
{
    /// <summary>
    /// Long operations, run on worker threads, remembered for the session.
    /// </summary>
    /// <remarks>
    /// **Cancellation only works before the work starts, and that is a property of Openness rather
    /// than a shortcut here.** A compile and a download are blocking calls into
    /// <c>Siemens.Engineering</c> that accept no cancellation token and expose no way to interrupt
    /// them. So a queued job can be cancelled and will never run; a running job is reported as not
    /// cancellable, which is the truth rather than a "cancelling" state that never resolves.
    ///
    /// The state transition <c>Queued -> Running</c> happens under the same lock
    /// <see cref="Cancel"/> takes, which is what makes cancelling before the start reliable instead
    /// of a race the caller loses about half the time.
    ///
    /// Jobs are kept for the lifetime of the session and never evicted. A session runs a handful of
    /// compiles and downloads, not thousands, and a job that disappeared before anyone read its
    /// result would defeat the point of having one.
    /// </remarks>
    public sealed class JobStore : IJobStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, Job> _jobs = new Dictionary<string, Job>(StringComparer.Ordinal);

        // Reused from the governance layer rather than declared again here. A second clock interface
        // for the same purpose would be worse than the small coupling: there would then be two ways
        // to make time testable and no reason to prefer either.
        private readonly ISystemClock _clock;
        private readonly IJobDispatcher _dispatcher;

        /// <summary>Creates an empty store.</summary>
        /// <param name="clock">Where the timestamps come from.</param>
        /// <param name="dispatcher">What runs the work away from the caller.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public JobStore(ISystemClock clock, IJobDispatcher dispatcher)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="tool"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="work"/> is null.</exception>
        public JobId Start(string tool, string target, Func<string> work)
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                throw new ArgumentException("A job must name the tool it runs", nameof(tool));
            }

            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            var job = new Job(JobId.Create(), tool, target ?? string.Empty, _clock.UtcNow);

            lock (_lock)
            {
                _jobs[job.Id.Value] = job;
            }

            // Fire and remember, not fire and forget: the outcome lands in the job, which is what
            // the caller polls. Nothing awaits this deliberately — returning immediately is the
            // entire purpose.
            _dispatcher.Dispatch(() => Execute(job, work));

            return job.Id;
        }

        /// <inheritdoc />
        /// <exception cref="PortalException">No such job.</exception>
        public JobRecord Status(JobId id)
        {
            lock (_lock)
            {
                return Require(id).Snapshot();
            }
        }

        /// <inheritdoc />
        /// <exception cref="PortalException">No such job.</exception>
        public JobRecord Cancel(JobId id)
        {
            lock (_lock)
            {
                var job = Require(id);

                if (job.State == JobState.Queued)
                {
                    job.Finish(JobState.Cancelled, "Cancelled before it started, so nothing ran.", _clock.UtcNow);
                }

                return job.Snapshot();
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<JobRecord> List()
        {
            lock (_lock)
            {
                return _jobs.Values
                    .Select(job => job.Snapshot())
                    .OrderByDescending(record => record.StartedAt)
                    .ToList();
            }
        }

        private void Execute(Job job, Func<string> work)
        {
            lock (_lock)
            {
                if (job.State != JobState.Queued)
                {
                    // Cancelled between being accepted and reaching a worker thread. This is the
                    // window the lock exists to make meaningful.
                    return;
                }

                job.Begin();
            }

            try
            {
                var detail = work();

                lock (_lock)
                {
                    job.Finish(JobState.Succeeded, detail, _clock.UtcNow);
                }
            }
            catch (Exception exception)
            {
                // Never swallowed and never rethrown: nothing is waiting on this thread, so
                // rethrowing would lose the reason entirely. It becomes the job's result, which is
                // the only place a caller can still read it.
                lock (_lock)
                {
                    job.Finish(JobState.Failed, exception.Message, _clock.UtcNow);
                }
            }
        }

        private Job Require(JobId id)
        {
            if (!_jobs.TryGetValue(id.Value, out var job))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"No job '{id}' in this session.");
            }

            return job;
        }

        /// <summary>
        /// One job's mutable state, reachable only under the store's lock.
        /// </summary>
        /// <remarks>
        /// The mutable field the repository's rules ask to be justified: a job is a thing that
        /// changes by definition, and the alternative — replacing an immutable record in the
        /// dictionary on each transition — puts the same mutation one level away without removing
        /// it. What callers receive is always an immutable <see cref="JobRecord"/>.
        /// </remarks>
        private sealed class Job
        {
            private readonly DateTimeOffset _startedAt;

            internal Job(JobId id, string tool, string target, DateTimeOffset startedAt)
            {
                Id = id;
                Tool = tool;
                Target = target;
                State = JobState.Queued;
                Detail = string.Empty;
                _startedAt = startedAt;
            }

            internal JobId Id { get; }

            internal JobState State { get; private set; }

            private string Tool { get; }

            private string Target { get; }

            private string Detail { get; set; }

            private DateTimeOffset? FinishedAt { get; set; }

            internal void Begin()
            {
                State = JobState.Running;
            }

            internal void Finish(JobState state, string detail, DateTimeOffset finishedAt)
            {
                State = state;
                Detail = detail;
                FinishedAt = finishedAt;
            }

            internal JobRecord Snapshot()
            {
                return new JobRecord(Id, Tool, Target, State, Detail, _startedAt, FinishedAt);
            }
        }
    }
}
