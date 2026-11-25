using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>A stored secret is a target of its own: a rule about the project or a device must not let a write use one.</remarks>
    [TestClass]
    public sealed class ChangeTargetSecretTests
    {
        [TestMethod]
        public void Secret_AName_IsUnderTheSecretPrefix()
        {
            Assert.AreEqual("secret/Plc_Cell", ChangeTarget.Secret(" Plc_Cell "));
        }

        [TestMethod]
        public void Secret_ARuleAllowingTheProject_DoesNotAllowUsingASecret()
        {
            var projectRule = TargetPattern.Parse(ChangeTarget.Project);
            var cellRule = TargetPattern.Parse("Cell/*");

            Assert.IsFalse(projectRule.Matches(ChangeTarget.Secret("Plc_Cell")));
            Assert.IsFalse(cellRule.Matches(ChangeTarget.Secret("Plc_Cell")));
        }

        [TestMethod]
        public void Secret_ARuleForTheClassSecrets_DoesNotReachTheWorkshopOne()
        {
            var classRule = TargetPattern.Parse(ChangeTarget.Secret("Class_*"));

            Assert.IsTrue(classRule.Matches(ChangeTarget.Secret("Class_Cell")));
            Assert.IsFalse(classRule.Matches(ChangeTarget.Secret("Workshop_Cell")));
        }

        /// <remarks>The store finds <c>plc_cell</c> as <c>Plc_Cell</c>, so a deny rule spelled one way must catch the other.</remarks>
        [TestMethod]
        public void Secret_AnotherCase_IsCaughtByADenyRule()
        {
            var denyRule = TargetPattern.Parse(ChangeTarget.Secret("Workshop_Cell"));

            Assert.IsTrue(denyRule.Matches(ChangeTarget.Secret("workshop_cell")));
        }
    }
}
