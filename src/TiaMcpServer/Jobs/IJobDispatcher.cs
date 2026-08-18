using System;

namespace TiaMcpServer.Jobs
{
    /// <summary>
    /// What actually runs a job's work, away from the caller.
    /// </summary>
    /// <remarks>
    /// An interface for the same reason <c>ISystemClock</c> is one: the behaviour worth asserting is
    /// that a job cancelled before it starts never runs, and that cannot be asserted against a
    /// thread pool that may have started the work before the test's next line. A test dispatcher
    /// holds the work until the test says so, and the assertion becomes exact instead of a sleep
    /// long enough to usually pass.
    /// </remarks>
    public interface IJobDispatcher
    {
        /// <summary>Runs the work somewhere other than here, and returns at once.</summary>
        /// <param name="work">What to run.</param>
        void Dispatch(Action work);
    }
}
