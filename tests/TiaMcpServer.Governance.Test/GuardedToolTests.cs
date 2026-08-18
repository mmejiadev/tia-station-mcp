using System;
using System.Collections.Generic;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The seam between a write tool and the guard. Its rules are about what the caller is told:
    /// a refusal must never look like a success, and a change that has not run yet must never
    /// return a payload suggesting it has.
    ///
    /// No TIA Portal here either — the tool's work is a lambda, so what the guard does with it can
    /// be checked without a project, which is the whole reason a refused change never reaches the
    /// Openness API in the first place.
    /// </remarks>
    [TestClass]
    public sealed class GuardedToolTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 17, 6, 0, 0, TimeSpan.Zero);
        private const string AllowedTarget = "PLC_0/Blocks/FB_Station";
        private const string ForbiddenTarget = "PLC_0/Safety/FB_Estop";

        [TestMethod]
        public void Run_WhenAllowed_ReturnsTheToolsOwnResponse()
        {
            var guard = GuardFor(OperationMode.Study);

            var response = GuardedTool.Run(
                guard,
                Request(AllowedTarget),
                () => new ResponseWriteScl(new[] { "FB_Station" }) { Message = "Generated 1 block(s)" },
                () => new ResponseWriteScl(Array.Empty<string>()));

            Assert.AreEqual(1, response.GeneratedBlocks.Count);
            Assert.AreEqual("Generated 1 block(s)", response.Message);
        }

        [TestMethod]
        public void Run_WhenRefused_NeverRunsTheWorkAndSaysWhy()
        {
            var guard = GuardFor(OperationMode.Study);
            var ran = false;

            var response = GuardedTool.Run(
                guard,
                Request(ForbiddenTarget),
                () => { ran = true; return new ResponseWriteScl(new[] { "FB_Estop" }); },
                () => new ResponseWriteScl(Array.Empty<string>()));

            Assert.IsFalse(ran, "a refused change must not reach the Openness API at all");
            Assert.AreEqual(0, response.GeneratedBlocks.Count);
            StringAssert.Contains(response.Message, ForbiddenTarget, StringComparison.Ordinal);
        }

        [TestMethod]
        public void Run_WhenRefused_ReportsFailureRatherThanThrowing()
        {
            // A refusal is the system working. Thrown, it would reach the caller as an operation
            // failure — something to retry — instead of a decision to respect.
            var guard = GuardFor(OperationMode.Study);

            var response = GuardedTool.Run(
                guard,
                Request(ForbiddenTarget),
                () => new ResponseWriteScl(Array.Empty<string>()),
                () => new ResponseWriteScl(Array.Empty<string>()));

            Assert.IsNotNull(response.Meta);
            Assert.AreEqual(false, response.Meta!["success"]!.GetValue<bool>());
            Assert.AreEqual(nameof(ChangeOutcomeKind.Refused), response.Meta["outcome"]!.GetValue<string>());
        }

        [TestMethod]
        public void Run_WhenConfirmationIsPending_ReturnsThePlanIdAndWritesNothing()
        {
            var guard = GuardFor(OperationMode.Workshop);
            var ran = false;

            var response = GuardedTool.Run(
                guard,
                Request(AllowedTarget),
                () => { ran = true; return new ResponseWriteScl(new[] { "FB_Station" }); },
                () => new ResponseWriteScl(Array.Empty<string>()));

            Assert.IsFalse(ran, "nothing may be written before a person confirms it");
            Assert.AreEqual(
                nameof(ChangeOutcomeKind.AwaitingConfirmation),
                response.Meta!["outcome"]!.GetValue<string>());
            Assert.AreNotEqual(string.Empty, response.Meta["planId"]!.GetValue<string>());
        }

        [TestMethod]
        public void Run_WhenTheToolReportsSuccessWithoutAResponse_IsAnError()
        {
            // The audit trail already says the change was applied, so a refusal-shaped answer here
            // would contradict the record. That is a defect in the tool, not a policy decision.
            var guard = GuardFor(OperationMode.Study);

            Assert.ThrowsException<PortalException>(() => GuardedTool.Run<ResponseWriteScl>(
                guard,
                Request(AllowedTarget),
                () => null!,
                () => new ResponseWriteScl(Array.Empty<string>())));
        }

        private static ChangeRequest Request(string target)
        {
            return new ChangeRequest("WriteScl", target, "FUNCTION_BLOCK ...", "test");
        }

        private static GuardedWrite GuardFor(OperationMode mode)
        {
            var policy = new WritePolicy(new Dictionary<OperationMode, ModeRules>
            {
                [OperationMode.Study] = new ModeRules(
                    OperationMode.Study,
                    new[] { "PLC_0/Blocks/*" },
                    Array.Empty<string>()),
                [OperationMode.Workshop] = new ModeRules(
                    OperationMode.Workshop,
                    new[] { AllowedTarget },
                    Array.Empty<string>())
            });

            return new GuardedWrite(
                new StubModeGate(mode),
                policy,
                new RecordingAuditTrail(),
                new ChangePlanStore(new FixedClock(Now)));
        }
    }
}
