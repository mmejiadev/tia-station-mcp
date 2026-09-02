using System;
using System.Collections.Generic;

namespace TiaMcpServer.Jobs
{
    /// <summary>
    /// Runs long operations away from the caller, and answers for them afterwards.
    /// </summary>
    /// <remarks>
    /// A compile or a download takes minutes and an agent blocked on one is useless. A download once
    /// blocked this project for thirteen hours with no way to ask what it was doing, which is the
    /// failure this exists to make impossible.
    /// </remarks>
    public interface IJobStore
    {
        /// <summary>Accepts a long operation and returns immediately.</summary>
        /// <param name="tool">The tool being run, for example <c>CompileSoftware</c>.</param>
        /// <param name="target">What it runs against.</param>
        /// <param name="work">The work. Its return value becomes the job's detail.</param>
        /// <returns>The identifier to poll with.</returns>
        JobId Start(string tool, string target, Func<string> work);

        /// <summary>Reports one job as it stands now.</summary>
        /// <param name="id">Which job.</param>
        /// <returns>The snapshot.</returns>
        JobRecord Status(JobId id);

        /// <summary>Cancels a job that has not started yet.</summary>
        /// <param name="id">Which job.</param>
        /// <returns>The snapshot after the attempt, cancelled or unchanged.</returns>
        JobRecord Cancel(JobId id);

        /// <summary>Every job this session has run, newest first.</summary>
        /// <returns>One snapshot per job.</returns>
        IReadOnlyList<JobRecord> List();
    }
}
