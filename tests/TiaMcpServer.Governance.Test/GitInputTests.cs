using System;
using System.IO;
using TiaMcpServer.Git;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <remarks>
    /// What reaches Git's command line is checked here and nowhere else, so these are the tests that say nothing a
    /// caller writes can become another argument or another command.
    /// </remarks>
    [TestClass]
    public sealed class GitInputTests
    {
        [TestMethod]
        [DataRow("main")]
        [DataRow("feature/conveyor")]
        [DataRow("fix-B2_sensor.v2")]
        public void RequireBranch_ASafeName_IsTaken(string branch)
        {
            Assert.AreEqual(branch, GitInput.RequireBranch(" " + branch + " "));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("-f")]
        [DataRow("--upload-pack=calc")]
        [DataRow("a b")]
        [DataRow("a;calc")]
        [DataRow("a&calc")]
        [DataRow("a\"b")]
        [DataRow("a..b")]
        [DataRow("a//b")]
        [DataRow("/a")]
        [DataRow("a/")]
        [DataRow("a.lock")]
        [DataRow("a/.hidden")]
        [DataRow("a.")]
        [DataRow("ñandú")]
        public void RequireBranch_AnUnsafeName_ThrowsInvalidParams(string branch)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => GitInput.RequireBranch(branch));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void RequireBranch_TooLong_ThrowsInvalidParams()
        {
            Assert.ThrowsException<PortalException>(() => GitInput.RequireBranch(new string('a', 101)));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("a\0b")]
        public void RequireMessage_EmptyOrBinary_ThrowsInvalidParams(string message)
        {
            Assert.ThrowsException<PortalException>(() => GitInput.RequireMessage(message));
        }

        [TestMethod]
        public void RequireMessage_TooLong_ThrowsInvalidParams()
        {
            Assert.ThrowsException<PortalException>(() => GitInput.RequireMessage(new string('a', GitInput.MaximumMessageLength + 1)));
        }

        [TestMethod]
        public void RequireMessage_QuotesAndNewLines_AreKept()
        {
            Assert.AreEqual("Conveyor: \"B2\"\nsecond line", GitInput.RequireMessage("Conveyor: \"B2\"\nsecond line"));
        }

        [TestMethod]
        [DataRow("status --short")]
        [DataRow("log\"")]
        [DataRow("a&b")]
        public void Run_AnArgumentThatIsNotOneToken_ThrowsInvalidParams(string argument)
        {
            var refusal = Assert.ThrowsException<PortalException>(() => new GitRunner(TimeSpan.FromSeconds(5)).Run(Path.GetTempPath(), new[] { "status", argument }));

            Assert.AreEqual(PortalErrorCode.InvalidParams, refusal.Code);
        }

        [TestMethod]
        public void Run_AnOptionWhereTheCommandGoes_ThrowsInvalidParams()
        {
            Assert.ThrowsException<PortalException>(() => new GitRunner(TimeSpan.FromSeconds(5)).Run(Path.GetTempPath(), new[] { "--exec-path=x", "status" }));
        }

        [TestMethod]
        public void Run_AFolderThatIsNotThere_ThrowsInvalidState()
        {
            var refusal = Assert.ThrowsException<PortalException>(() => new GitRunner(TimeSpan.FromSeconds(5)).Run(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), new[] { "status" }));

            Assert.AreEqual(PortalErrorCode.InvalidState, refusal.Code);
        }
    }
}
