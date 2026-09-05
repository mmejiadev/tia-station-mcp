using System;

namespace TiaMcpServer.Jobs
{
    /// <summary>
    /// What a caller can see about one long operation.
    /// </summary>
    /// <remarks>
    /// A snapshot, not a live view: it is built when the store is asked and never changes
    /// afterwards, so nothing here has a setter. Reporting a mutable object would let the state a
    /// caller decided on shift underneath the decision.
    /// </remarks>
    public sealed class JobRecord
    {
        /// <summary>Describes one job as it stood when asked.</summary>
        /// <param name="id">Which job.</param>
        /// <param name="tool">The tool it is running, for example <c>CompileSoftware</c>.</param>
        /// <param name="target">What it is running against.</param>
        /// <param name="state">Where it has got to.</param>
        /// <param name="detail">Its result, or the reason it failed. Empty while it runs.</param>
        /// <param name="startedAt">When it was accepted, in UTC.</param>
        /// <param name="finishedAt">When it stopped, or null while it runs.</param>
        /// <exception cref="ArgumentException"><paramref name="tool"/> is empty.</exception>
        public JobRecord(
            JobId id,
            string tool,
            string target,
            JobState state,
            string detail,
            DateTimeOffset startedAt,
            DateTimeOffset? finishedAt)
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                throw new ArgumentException("A job must name the tool it runs", nameof(tool));
            }

            Id = id;
            Tool = tool;
            Target = target ?? string.Empty;
            State = state;
            Detail = detail ?? string.Empty;
            StartedAt = startedAt;
            FinishedAt = finishedAt;
        }

        /// <summary>Which job.</summary>
        public JobId Id { get; }

        /// <summary>The tool it runs.</summary>
        public string Tool { get; }

        /// <summary>What it runs against.</summary>
        public string Target { get; }

        /// <summary>Where it has got to.</summary>
        public JobState State { get; }

        /// <summary>Its result, or the reason it failed. Empty while it runs.</summary>
        public string Detail { get; }

        /// <summary>When it was accepted, in UTC.</summary>
        public DateTimeOffset StartedAt { get; }

        /// <summary>When it stopped, or null while it runs.</summary>
        public DateTimeOffset? FinishedAt { get; }

        /// <summary>Whether it has stopped, whatever the reason.</summary>
        public bool IsFinished =>
            State == JobState.Succeeded || State == JobState.Failed || State == JobState.Cancelled;

        /// <summary>
        /// Whether cancelling it would do anything.
        /// </summary>
        /// <remarks>
        /// Only while queued. Openness offers no way to interrupt a compile or a download once it
        /// has begun, so reporting a running job as cancellable would be a promise nothing can
        /// keep — and the caller would act on it.
        /// </remarks>
        public bool IsCancellable => State == JobState.Queued;
    }
}
