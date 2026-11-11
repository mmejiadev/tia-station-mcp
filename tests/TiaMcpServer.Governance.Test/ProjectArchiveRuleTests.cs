using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    [TestClass]
    public sealed class ProjectArchiveRuleTests
    {
        [TestMethod]
        public void ArchiveFileName_PlainName_GetsThePortalsExtension()
        {
            var fileName = ProjectArchiveRule.ArchiveFileName(" Motor_Samreen ", 21);

            Assert.AreEqual("Motor_Samreen.zap21", fileName);
        }

        [TestMethod]
        [DataRow("Motor.zap20")]
        [DataRow("Motor.ZAP21")]
        [DataRow("Motor.zap")]
        public void ArchiveFileName_NameWithAnArchiveExtension_IsInvalidParams(string archiveName)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => ProjectArchiveRule.ArchiveFileName(archiveName, 21));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow("..")]
        [DataRow(@"Class\Motor")]
        [DataRow("Class/Motor")]
        public void ArchiveFileName_EmptyOrAPath_IsInvalidParams(string archiveName)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => ProjectArchiveRule.ArchiveFileName(archiveName, 21));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }
    }
}
