namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One long operation, as it stood when asked.</summary>
    public sealed class ResponseJob : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="jobId">Which job.</param>
        /// <param name="tool">The tool it runs.</param>
        /// <param name="target">What it runs against.</param>
        /// <param name="state">Queued, Running, Succeeded, Failed or Cancelled.</param>
        /// <param name="detail">Its result, or the reason it failed. Empty while it runs.</param>
        /// <param name="isCancellable">Whether cancelling it would do anything.</param>
        public ResponseJob(string jobId, string tool, string target, string state, string detail, bool isCancellable)
        {
            JobId = jobId;
            Tool = tool;
            Target = target;
            State = state;
            Detail = detail;
            IsCancellable = isCancellable;
        }

        /// <summary>Which job. Poll with this.</summary>
        public string JobId { get; }

        /// <summary>The tool it runs.</summary>
        public string Tool { get; }

        /// <summary>What it runs against.</summary>
        public string Target { get; }

        /// <summary>Queued, Running, Succeeded, Failed or Cancelled.</summary>
        public string State { get; }

        /// <summary>Its result, or the reason it failed. Empty while it runs.</summary>
        public string Detail { get; }

        /// <summary>
        /// Whether cancelling it would do anything. True only while queued: Openness cannot
        /// interrupt a compile or a download that has started.
        /// </summary>
        public bool IsCancellable { get; }
    }
}
