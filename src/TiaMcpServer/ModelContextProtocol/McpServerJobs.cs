using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Long operations the caller polls instead of waiting on.
    ///
    /// A bulk export can take minutes, and an MCP call that blocks that long looks like a dead
    /// server. These hand back a job identifier and let the caller ask.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetJobStatus"), Description("Ask how a long operation started with runAsJob is going. State is Queued, Running, Succeeded, Failed or Cancelled; detail carries the result once it has finished.")]
        public static ResponseJob GetJobStatus(
            [Description("jobId: the id the tool that started the job returned")] string jobId)
        {
            try
            {
                return Describe(JobStore.Status(Jobs.JobId.Parse(jobId)), "Job");
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to report on job '{jobId}'");
            }
        }

        [McpServerTool(Name = "CancelJob"), Description("Cancel a long operation that has not started yet. A job already inside Openness cannot be interrupted — a compile and a download accept no cancellation — and this reports that rather than pretending to stop it.")]
        public static ResponseJob CancelJob(
            [Description("jobId: the id the tool that started the job returned")] string jobId)
        {
            try
            {
                var job = JobStore.Cancel(Jobs.JobId.Parse(jobId));

                return Describe(
                    job,
                    job.State == Jobs.JobState.Cancelled
                        ? "Cancelled"
                        : "Not cancelled; it is past the point where that is possible. Job");
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to cancel job '{jobId}'");
            }
        }

        [McpServerTool(Name = "ListJobs"), Description("List every long operation this session has run, newest first, with what became of it.")]
        public static ResponseJobs ListJobs()
        {
            try
            {
                var items = JobStore.List().Select(ToResponse).ToList();

                return new ResponseJobs(items)
                {
                    Message = items.Count == 0
                        ? "No long operations have been started in this session."
                        : $"{items.Count} job(s), newest first.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = items.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed to list the jobs");
            }
        }

        private static ResponseJob ToResponse(Jobs.JobRecord job)
        {
            return new ResponseJob(
                job.Id.Value,
                job.Tool,
                job.Target,
                job.State.ToString(),
                job.Detail,
                job.IsCancellable);
        }

        private static ResponseJob Describe(Jobs.JobRecord job, string prefix)
        {
            var response = ToResponse(job);

            response.Message = $"{prefix} '{job.Id}' ({job.Tool} on '{job.Target}'): {job.State}." +
                (job.Detail.Length == 0 ? string.Empty : $" {job.Detail}");
            response.Meta = new JsonObject
            {
                ["timestamp"] = DateTime.Now,
                ["success"] = job.State != Jobs.JobState.Failed,
                ["jobId"] = job.Id.Value,
                ["state"] = job.State.ToString(),
                ["isFinished"] = job.IsFinished,
                ["isCancellable"] = job.IsCancellable
            };

            return response;
        }
    }
}
