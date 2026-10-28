using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// On 2026-10-15 the server attached to a leftover headless TIA Portal while the user's window
    /// held her project. These pin the order that would have chosen her window instead.
    /// </remarks>
    [TestClass]
    public sealed class PortalProcessSelectorTests
    {
        private const string Hopper = @"C:\Projects\Hopper\Hopper.ap20";
        private const string Motor = @"C:\Projects\Motor\Motor.ap20";

        [TestMethod]
        public void Choose_NothingRunning_StartsANewOne()
        {
            var choice = PortalProcessSelector.Choose([], Hopper);

            Assert.IsNull(choice.Process);
            Assert.AreEqual(PortalProcessChoiceReason.NoneRunning, choice.Reason);
        }

        [TestMethod]
        public void Choose_AHeadlessLeftoverListedBeforeTheUsersWindow_ChoosesTheWindow()
        {
            var leftover = new PortalProcessInfo(1, false, string.Empty);
            var window = new PortalProcessInfo(2, true, Hopper);

            var choice = PortalProcessSelector.Choose([leftover, window], string.Empty);

            Assert.AreSame(window, choice.Process);
            Assert.AreEqual(PortalProcessChoiceReason.HasUserInterface, choice.Reason);
        }

        [TestMethod]
        public void Choose_AHeadlessProcessHoldsTheRequestedProject_ChoosesItOverAWindow()
        {
            var window = new PortalProcessInfo(1, true, Motor);
            var holder = new PortalProcessInfo(2, false, Hopper);

            var choice = PortalProcessSelector.Choose([window, holder], Hopper);

            Assert.AreSame(holder, choice.Process);
            Assert.AreEqual(PortalProcessChoiceReason.HoldsRequestedProject, choice.Reason);
        }

        [TestMethod]
        public void Choose_TwoWindows_PrefersTheOneWithAProject()
        {
            var empty = new PortalProcessInfo(1, true, string.Empty);
            var working = new PortalProcessInfo(2, true, Motor);

            var choice = PortalProcessSelector.Choose([empty, working], Hopper);

            Assert.AreSame(working, choice.Process);
        }

        [TestMethod]
        public void Choose_OnlyHeadlessProcesses_PrefersTheOneWithAProject()
        {
            var empty = new PortalProcessInfo(1, false, string.Empty);
            var working = new PortalProcessInfo(2, false, Motor);

            var choice = PortalProcessSelector.Choose([empty, working], string.Empty);

            Assert.AreSame(working, choice.Process);
            Assert.AreEqual(PortalProcessChoiceReason.WithoutUserInterface, choice.Reason);
        }
    }
}
