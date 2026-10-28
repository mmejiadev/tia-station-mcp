using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// The safety rule here: opening a project never closes one held by a TIA Portal the server did
    /// not start. Before it, OpenProject closed whatever was open first, which in the user's own
    /// window is her project, unsaved changes included.
    /// </remarks>
    [TestClass]
    public sealed class ProjectOpeningRuleTests
    {
        private const string Hopper = @"C:\Projects\Hopper\Hopper.ap20";
        private const string Motor = @"C:\Projects\Motor\Motor.ap20";

        [TestMethod]
        public void Decide_AnotherProjectInSomebodyElsesPortal_RefusesRatherThanClosingIt()
        {
            var request = new ProjectOpeningRequest(Hopper, Motor, ownsPortal: false, []);

            var decision = ProjectOpeningRule.Decide(request);

            Assert.AreEqual(ProjectOpeningAction.Refuse, decision.Action);
        }

        [TestMethod]
        public void Decide_AnotherProjectInTheServersOwnPortal_ReplacesIt()
        {
            var request = new ProjectOpeningRequest(Hopper, Motor, ownsPortal: true, []);

            var decision = ProjectOpeningRule.Decide(request);

            Assert.AreEqual(ProjectOpeningAction.ReplaceOwnProject, decision.Action);
        }

        [TestMethod]
        public void Decide_TheProjectIsAlreadyOpenHere_UsesIt()
        {
            var request = new ProjectOpeningRequest(Hopper, Hopper.ToUpperInvariant(), ownsPortal: false, []);

            var decision = ProjectOpeningRule.Decide(request);

            Assert.AreEqual(ProjectOpeningAction.UseOpenProject, decision.Action);
        }

        /// <remarks>
        /// The 2026-10-15 failure: the project was open in the user's window, the server was attached
        /// to another process, and opening it there failed because TIA Portal had it locked.
        /// </remarks>
        [TestMethod]
        public void Decide_AnotherProcessHoldsTheProject_AttachesToThatProcess()
        {
            var holder = new PortalProcessInfo(7, true, Hopper);
            var request = new ProjectOpeningRequest(Hopper, string.Empty, ownsPortal: true, [holder]);

            var decision = ProjectOpeningRule.Decide(request);

            Assert.AreEqual(ProjectOpeningAction.AttachToHolder, decision.Action);
            Assert.AreSame(holder, decision.Holder);
        }

        [TestMethod]
        public void Decide_NothingOpenHereAndNowhereElse_OpensHere()
        {
            var request = new ProjectOpeningRequest(Hopper, string.Empty, ownsPortal: false, []);

            var decision = ProjectOpeningRule.Decide(request);

            Assert.AreEqual(ProjectOpeningAction.OpenHere, decision.Action);
        }

        /// <remarks>
        /// Two projects with one name in two folders: matching by name, as the server used to,
        /// would have taken the open one for the one asked for and written into it.
        /// </remarks>
        [TestMethod]
        public void Decide_AProjectWithTheSameNameInAnotherFolder_IsNotTakenForTheRequestedOne()
        {
            var request = new ProjectOpeningRequest(Hopper, @"D:\Copy\Hopper\Hopper.ap20", ownsPortal: false, []);

            var decision = ProjectOpeningRule.Decide(request);

            Assert.AreEqual(ProjectOpeningAction.Refuse, decision.Action);
        }
    }
}
