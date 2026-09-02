using System;
using System.Collections.Generic;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>One test per rule the whitelist is supposed to enforce.</remarks>
    [TestClass]
    public sealed class WritePolicyTests
    {
        [TestMethod]
        public void Decide_TargetOnNoList_IsRefused()
        {
            // Deny by default. The rule the whole whitelist rests on: what nobody listed is not
            // permitted, however harmless it looks.
            var policy = PolicyFor(OperationMode.Study, allow: new[] { "PLC_0/Blocks/*" }, deny: Array.Empty<string>());

            var decision = policy.Decide(OperationMode.Study, "PLC_0/Safety/FB_Estop");

            Assert.IsFalse(decision.IsAllowed);
            StringAssert.Contains(decision.Reason, "on no allow list", StringComparison.Ordinal);
        }

        [TestMethod]
        public void Decide_DenyBeatsAllow()
        {
            // So a broad rule can be narrowed without being rewritten — and so narrowing is the
            // easy edit, which is the one people actually make.
            var policy = PolicyFor(OperationMode.Study, allow: new[] { "PLC_0/*" }, deny: new[] { "PLC_0/Safety/*" });

            Assert.IsTrue(policy.Decide(OperationMode.Study, "PLC_0/Blocks/FB_Station").IsAllowed);
            Assert.IsFalse(policy.Decide(OperationMode.Study, "PLC_0/Safety/FB_Estop").IsAllowed);
        }

        [TestMethod]
        public void Decide_ModeWithNoRules_RefusesEverything()
        {
            // A policy that says nothing about a mode has not authorised that mode.
            var policy = PolicyFor(OperationMode.Study, allow: new[] { "PLC_0/*" }, deny: Array.Empty<string>());

            var decision = policy.Decide(OperationMode.Workshop, "PLC_0/Blocks/FB_Station");

            Assert.IsFalse(decision.IsAllowed);
            StringAssert.Contains(decision.Reason, "no policy is configured", StringComparison.Ordinal);
        }

        [TestMethod]
        public void WorkshopRules_WithAWildcard_AreRefusedWhenLoaded()
        {
            // A wildcard is a rule about targets nobody enumerated. Tolerable when the worst case
            // is a broken simulation; not when the target can move. Refused at load rather than at
            // use, so a policy that cannot be honoured never governs any work.
            var exception = Assert.ThrowsException<PortalException>(
                () => new ModeRules(OperationMode.Workshop, new[] { "PLC_0/Tags/*" }, Array.Empty<string>()));

            Assert.AreEqual(PortalErrorCode.InvalidParams, exception.Code);
            StringAssert.Contains(exception.Message, "may not contain wildcards", StringComparison.Ordinal);
        }

        [TestMethod]
        public void WorkshopRules_WrittenOutInFull_AreAccepted()
        {
            var rules = new ModeRules(OperationMode.Workshop, new[] { "PLC_0/Tags/Conveyor_Start" }, Array.Empty<string>());

            Assert.IsTrue(rules.Decide("PLC_0/Tags/Conveyor_Start").IsAllowed);
            Assert.IsFalse(rules.Decide("PLC_0/Tags/Conveyor_Stop").IsAllowed);
        }

        [TestMethod]
        public void Load_MissingFile_DeniesEverything()
        {
            // Inconvenient exactly once, when the project is set up, and correct every time after.
            var policy = WritePolicy.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "policy.json"));

            Assert.IsFalse(policy.Decide(OperationMode.Study, "PLC_0/Blocks/FB_Station").IsAllowed);
            Assert.IsFalse(policy.Governs(OperationMode.Study));
        }

        [TestMethod]
        public void Load_UnreadableFile_RefusesRatherThanRunningUnprotected()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{ this is not json");

            try
            {
                var exception = Assert.ThrowsException<PortalException>(() => WritePolicy.Load(path));

                StringAssert.Contains(exception.Message, "Refusing to run without one", StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_RealFile_ReadsBothSections()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(
                path,
                "{\"study\":{\"allow\":[\"PLC_0/*\"],\"deny\":[]}," +
                "\"workshop\":{\"allow\":[\"PLC_0/Tags/Conveyor_Start\"],\"deny\":[]}}");

            try
            {
                var policy = WritePolicy.Load(path);

                Assert.IsTrue(policy.Decide(OperationMode.Study, "PLC_0/Blocks/FB_Station").IsAllowed);
                Assert.IsTrue(policy.Decide(OperationMode.Workshop, "PLC_0/Tags/Conveyor_Start").IsAllowed);
                Assert.IsFalse(policy.Decide(OperationMode.Workshop, "PLC_0/Blocks/FB_Station").IsAllowed);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Decide_ATargetWithATrailingNewline_IsStillOffTheAllowList()
        {
            // In .NET the $ anchor also matches immediately before a trailing newline, so the
            // pattern "PLC_0/Blocks/*" used to accept a target carrying one. A whitelist that
            // matches a string it was never shown is not a whitelist. Audit of 2026-09-02.
            var policy = PolicyFor(OperationMode.Study, allow: new[] { "PLC_0/Blocks/*" }, deny: Array.Empty<string>());

            var decision = policy.Decide(OperationMode.Study, "PLC_0/Blocks/FB_Station" + (char)10);

            Assert.IsFalse(decision.IsAllowed, decision.Reason);
        }

        private static WritePolicy PolicyFor(OperationMode mode, string[] allow, string[] deny)
        {
            return new WritePolicy(new Dictionary<OperationMode, ModeRules>
            {
                [mode] = new ModeRules(mode, allow, deny)
            });
        }
    }
}
