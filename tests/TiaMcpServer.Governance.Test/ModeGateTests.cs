using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// One explicit test per safety rule, as <c>CLAUDE.md</c> requires of this layer. Incidental
    /// coverage from a test about something else does not count: a rule nobody asserts is a rule
    /// that quietly stops holding.
    ///
    /// These run **without TIA Portal**, which is why they live in their own project rather than
    /// alongside the Openness tests: that assembly starts a portal in <c>[AssemblyInitialize]</c>
    /// for every test in it. A safety rule that can only be checked on a licensed machine is a
    /// safety rule that stops being checked.
    /// </remarks>
    [TestClass]
    public sealed class ModeGateTests
    {
        [TestMethod]
        public void ForStudy_IsTheDefault_AndConfirmsAutomatically()
        {
            var gate = ModeGate.ForStudy();

            Assert.AreEqual(OperationMode.Study, gate.Mode);
            Assert.AreEqual(Confirmation.Automatic, gate.RequiredConfirmation);
            Assert.IsTrue(gate.IsSimulationOnly, "Study Mode must not be able to reach physical hardware");
        }

        [TestMethod]
        public void ForWorkshop_InTheDefaultBuild_IsUnreachable()
        {
            // Layer 0, and the rule that makes every other layer a second line rather than the
            // only one — so it is asserted rather than assumed. If this test ever starts failing,
            // the default build has grown the ability to command physical hardware.
            var exception = Assert.ThrowsException<PortalException>(
                () => ModeGate.ForWorkshop(ModeGate.WorkshopConfirmationPhrase));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
            StringAssert.Contains(exception.Message, "not present in this build", StringComparison.Ordinal);
        }

        [TestMethod]
        public void ForWorkshop_WithoutTheConfirmationPhrase_IsAlsoRefused()
        {
            // Layer 1, checked here too. In the default build layer 0 refuses first, so what this
            // proves today is that a wrong phrase never gets further than a right one. In a
            // WORKSHOP_MODE build it becomes the real assertion of the phrase check, and it fails
            // there rather than being silently absent.
            Assert.ThrowsException<PortalException>(() => ModeGate.ForWorkshop("go on then"));
        }

        [TestMethod]
        public void ConfirmationFor_Workshop_RequiresAPerson()
        {
            Assert.AreEqual(Confirmation.Manual, ModeGate.ConfirmationFor(OperationMode.Workshop));
        }

        [TestMethod]
        public void ConfirmationFor_UnrecognisedMode_RefusesInsteadOfDefaulting()
        {
            // The fail-closed rule itself. A new member of OperationMode that nobody decided a
            // confirmation for must break loudly, not inherit Study's automatic confirmation.
            var exception = Assert.ThrowsException<PortalException>(
                () => ModeGate.ConfirmationFor((OperationMode)99));

            Assert.AreEqual(PortalErrorCode.InvalidState, exception.Code);
            StringAssert.Contains(exception.Message, "Refusing rather than assuming", StringComparison.Ordinal);
        }
    }
}
