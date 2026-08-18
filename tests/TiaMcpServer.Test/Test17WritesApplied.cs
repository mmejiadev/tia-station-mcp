using System;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// The MCP write tools on the path where the policy says yes.
    /// </summary>
    /// <remarks>
    /// <c>Test16GuardedWrites</c> covers every write tool under a policy that denies everything, which
    /// left a hole worth naming: **nothing exercised the allowed path through the MCP layer at all.**
    /// Every other test in the suite calls <c>Portal</c> directly, so the code between a tool and the
    /// portal — the backup registry it asks for a location, the job store it hands long work to — was
    /// written and never run. That is the same gap that made <c>Test16</c>'s first execution find two
    /// defects reading the code had not.
    ///
    /// So this class asserts the seam, not the Openness behaviour underneath it: that a write really
    /// leaves its previous state where <c>ListBackups</c> can find it, and that a job really carries a
    /// result back to <c>GetJobStatus</c>.
    ///
    /// <c>[DoNotParallelize]</c> like the other classes that open the shared project: they cannot
    /// overlap.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public sealed class Test17WritesApplied
    {
        private const string ValidScl = @"FUNCTION ""FC_WrittenThroughTheGuard"" : Void
VERSION : 0.1
BEGIN
    ; // deliberately empty body
END_FUNCTION
";

        [TestInitialize]
        public void TestInit()
        {
            AssemblyHooks.SharedPortal.OpenProject(AssemblyHooks.ProjectPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AssemblyHooks.SharedPortal.CloseProject();
        }

        [TestMethod]
        public void WriteScl_AllowedTarget_LeavesItsBackupWhereListBackupsFindsIt()
        {
            // The property the mandatory backupDirectory parameter used to provide and could not: the
            // copy is somewhere a person who did not take it can find.
            var response = McpServer.WriteScl(Settings.Project1PlcSoftwarePath0, ValidScl);

            Assert.IsTrue(
                response.GeneratedBlocks.Count > 0,
                $"the write did not run: {response.Message}");

            var backup = McpServer.ListBackups().Items
                .FirstOrDefault(item => item.Tool == "WriteScl" && item.Target == Settings.Project1PlcSoftwarePath0);

            Assert.IsNotNull(backup, "a write that overwrites blocks must leave a backup the registry knows about");
            Assert.IsTrue(backup!.FileCount > 0, "the backup directory was allocated but nothing was exported into it");
        }

        [TestMethod]
        public void CompileSoftware_AsJob_ReturnsAHandleAndThenTheResult()
        {
            var accepted = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0, string.Empty, runAsJob: true);
            var jobId = accepted.Meta?["jobId"]?.GetValue<string>();

            Assert.IsFalse(string.IsNullOrEmpty(jobId), $"runAsJob returned no job id: {accepted.Message}");
            Assert.AreEqual(false, accepted.Meta?["isFinished"]?.GetValue<bool>());

            var job = WaitUntilFinished(jobId!);

            Assert.AreEqual("Succeeded", job.State, $"the compile job did not succeed: {job.Detail}");
            StringAssert.Contains(job.Detail, Settings.Project1PlcSoftwarePath0, StringComparison.Ordinal);
        }

        [TestMethod]
        public void ListJobs_AfterAJobHasRun_FindsIt()
        {
            var accepted = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0, string.Empty, runAsJob: true);
            var jobId = accepted.Meta?["jobId"]?.GetValue<string>();

            WaitUntilFinished(jobId!);

            var jobs = McpServer.ListJobs().Items;

            Assert.IsTrue(jobs.Any(job => job.JobId == jobId), "a job that ran must still be listed afterwards");
        }

        [TestMethod]
        public void GetJobStatus_AnIdThatIsNotAJobId_IsRejectedAsBadInput()
        {
            // Cheap, and it covers the mapping: a malformed id is the caller's mistake, not an
            // internal error, so it must not reach them as one.
            var exception = Assert.ThrowsException<global::ModelContextProtocol.McpException>(
                () => McpServer.GetJobStatus("not-a-job-id"));

            Assert.AreEqual(global::ModelContextProtocol.McpErrorCode.InvalidParams, exception.ErrorCode);
        }

        [TestMethod]
        public void CancelJob_OneThatAlreadyFinished_SaysSoRatherThanPretending()
        {
            var accepted = McpServer.CompileSoftware(Settings.Project1PlcSoftwarePath0, string.Empty, runAsJob: true);
            var jobId = accepted.Meta?["jobId"]?.GetValue<string>();

            WaitUntilFinished(jobId!);

            var cancelled = McpServer.CancelJob(jobId!);

            Assert.AreEqual("Succeeded", cancelled.State, "cancelling must not rewrite what already happened");
            Assert.IsFalse(cancelled.IsCancellable);
        }

        /// <summary>Polls a job until it stops, or fails the test.</summary>
        /// <remarks>
        /// This waits on wall-clock time because the real dispatcher is the thread pool and a real
        /// compile takes seconds. It is the wiring under test here, not the store's logic, which
        /// <c>JobStoreTests</c> asserts without waiting at all. The bound is generous so a loaded
        /// machine reports a timeout with a reason rather than a flake.
        /// </remarks>
        private static ResponseJob WaitUntilFinished(string jobId)
        {
            var deadline = DateTime.UtcNow.AddMinutes(3);

            while (DateTime.UtcNow < deadline)
            {
                var job = McpServer.GetJobStatus(jobId);

                if (job.Meta?["isFinished"]?.GetValue<bool>() == true)
                {
                    return job;
                }

                System.Threading.Thread.Sleep(100);
            }

            Assert.Fail($"job '{jobId}' had not finished after 3 minutes");

            throw new InvalidOperationException("unreachable");
        }
    }
}
