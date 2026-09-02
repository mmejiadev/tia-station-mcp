using System;
using System.Threading.Tasks;

namespace TiaMcpServer.Jobs
{
    /// <summary>Runs jobs on the thread pool.</summary>
    public sealed class ThreadPoolJobDispatcher : IJobDispatcher
    {
        /// <inheritdoc />
        /// <exception cref="ArgumentNullException"><paramref name="work"/> is null.</exception>
        public void Dispatch(Action work)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            // Not awaited on purpose: returning before the work finishes is the whole point, and the
            // work itself never throws — JobStore turns a failure into the job's result, because
            // nothing is waiting here to catch one.
            Task.Run(work);
        }
    }
}
