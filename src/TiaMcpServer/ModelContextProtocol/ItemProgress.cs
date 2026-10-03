using System;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Turns the portal layer's item count into progress notifications, in order.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Progress{T}"/>: it posts each report to the thread pool, so notifications can
    /// arrive out of order and after the tool has answered. The export runs on a worker thread with
    /// no synchronisation context, so waiting for each notification there is safe, and ReportAsync
    /// never throws.
    /// </remarks>
    internal sealed class ItemProgress : IProgress<int>
    {
        private readonly ProgressReporter _reporter;
        private readonly int _total;
        private readonly string _noun;

        public ItemProgress(ProgressReporter reporter, int total, string noun)
        {
            _reporter = reporter;
            _total = total;
            _noun = noun;
        }

        public void Report(int value)
        {
            _reporter.ReportAsync(value, _total, $"{value} of {_total} {_noun}").GetAwaiter().GetResult();
        }
    }
}
